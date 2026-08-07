#nullable enable
using System.Linq;
using System.Threading;
using Cysharp.Threading.Tasks;
using Novel.Runtime;
using UnityEditor;
using UnityEngine;

namespace Novel.Editor
{
    // 全 .rb シナリオを検証する:
    //   1. .mrb バイトコード（mrubycs-compiler のサブアセット）を生成できているか（構文エラーの洗い出し）
    //   2. 使っているキー（キャラ/立ち絵/画像/音/構図）が実在するか（ScenarioKeyValidator のスタブ実行。
    //      未配線・キー間違いは実行時に無音 no-op で流れる設計のため、ここで編集時に警告する）
    public static class ScenarioValidator
    {
        [MenuItem("Novel/Validate Scenarios")]
        public static void Validate() => ValidateAsync().Forget();

        private static async UniTask ValidateAsync()
        {
            int total = 0;
            int failed = 0;
            int keyIssues = 0;
            var known = ScenarioKeyValidator.BuildKnownKeys();

            foreach (var guid in AssetDatabase.FindAssets("t:TextAsset"))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                if (!path.EndsWith(".rb")) continue;

                // コンパイル検証は preamble / ルビ辞書 / テスト含む全 .rb が対象
                total++;
                var bytecode = AssetDatabase.LoadAllAssetsAtPath(path)
                    .OfType<TextAsset>()
                    .FirstOrDefault(a => a.name.EndsWith(".mrb"))?.bytes;
                if (bytecode == null || bytecode.Length == 0)
                {
                    failed++;
                    Debug.LogError($"[Novel] バイトコード未生成（コンパイル失敗の可能性）: {path}");
                    continue;
                }

                // 実行 (キー) 検証はシナリオでない .rb を除外する。フォルダ丸ごとではなく規約上の
                // 既定パス 2 ファイルに絞る (Resources/Novel/ 全体を除外すると、game がそこに置いた
                // シナリオまで黙って素通りするため)。テスト用シナリオは実行時に存在しないため対象外
                if (path.EndsWith("/Resources/Novel/Preamble.rb") ||
                    path.EndsWith("/Resources/Novel/ruby.rb") ||
                    path.Contains("/Tests/")) continue;

                var collected = await ScenarioKeyValidator.CollectAsync(
                    new PrecompiledScenarioSource(bytecode),
                    System.IO.Path.GetFileNameWithoutExtension(path));
                keyIssues += ScenarioKeyValidator.Report(path, System.IO.File.ReadAllText(path), collected, known);
            }

            // 情報源が無く検証をスキップした種別を明示する (「警告 0 = 全部確認済み」と誤読させない)
            var skipped = new System.Collections.Generic.List<string>();
            if (known.Speakers == null) skipped.Add("キャラ(カタログなし)");
            if (known.ImageKeys == null) skipped.Add("画像(スプライトなし)");
            if (known.SeKeys == null) skipped.Add("SE");
            if (known.BgmKeys == null) skipped.Add("BGM");
            if (known.Layouts == null) skipped.Add("構図");
            var note = skipped.Count > 0
                ? $"（情報源が無いためスキップ: {string.Join("・", skipped)}。SE/BGM/構図は列挙を実装して一度再生すると検証対象になる）"
                : "";
            Debug.Log($"[Novel] シナリオ検証完了: {total} 件中 {failed} 件が未コンパイル・未定義キー {keyIssues} 件{note}");
        }

        // 検証対象ファイルの .mrb サブアセットをそのまま返す IScenarioSource
        private sealed class PrecompiledScenarioSource : IScenarioSource
        {
            private readonly byte[] _bytecode;
            public PrecompiledScenarioSource(byte[] bytecode) => _bytecode = bytecode;
            public UniTask<byte[]?> LoadBytecodeAsync(string scenarioKey, CancellationToken ct)
                => UniTask.FromResult<byte[]?>(_bytecode);
        }
    }
}
