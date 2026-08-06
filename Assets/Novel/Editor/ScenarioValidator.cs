#nullable enable
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Novel.Editor
{
    // 全 .rb シナリオを検証する:
    //   1. .mrb バイトコード（mrubycs-compiler のサブアセット）を生成できているか（構文エラーの洗い出し）
    //   2. 使っているキー（キャラ/立ち絵/画像/音/構図）が実在するか（ScenarioKeyValidator。
    //      未配線・キー間違いは実行時に無音 no-op で流れる設計のため、ここで編集時に警告する）
    public static class ScenarioValidator
    {
        [MenuItem("Novel/Validate Scenarios")]
        public static void Validate()
        {
            int total = 0;
            int failed = 0;
            int keyIssues = 0;
            var known = ScenarioKeyValidator.BuildKnownKeys();

            foreach (var guid in AssetDatabase.FindAssets("t:TextAsset"))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                if (!path.EndsWith(".rb")) continue;

                total++;
                var hasBytecode = AssetDatabase.LoadAllAssetsAtPath(path)
                    .Any(a => a != null && a.name.EndsWith(".mrb"));
                if (!hasBytecode)
                {
                    failed++;
                    Debug.LogError($"[Novel] バイトコード未生成（コンパイル失敗の可能性）: {path}");
                }

                keyIssues += ScenarioKeyValidator.Validate(path, System.IO.File.ReadAllText(path), known);
            }

            var audioNote = known.SeKeys == null ? "（音/構図は未キャプチャのためスキップ。一度再生すると検証対象になる）" : "";
            Debug.Log($"[Novel] シナリオ検証完了: {total} 件中 {failed} 件が未コンパイル・未定義キー {keyIssues} 件{audioNote}");
        }
    }
}
