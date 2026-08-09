#nullable enable
using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using MRubyCS;
using Novel.Commands;
using Novel.Runtime;
using Novel.View;
using UnityEditor;
using UnityEngine;
using VitalRouter;

namespace Novel.Editor
{
    // シナリオが使うキー (キャラ/立ち絵/画像/音/構図) が実在するかの突き合わせ (project-reference ADR)。
    //
    // キーの抽出は正規表現パースではなく「スタブ実行」で行う: コンパイル済み .mrb を実 preamble 込みで
    // 早送り実行し、Router に流れる型付きコマンド (BackgroundCommand 等) からキーを記録する。
    // パースの正確さ (コメント/クォート/複数行/#{} 補間) を mruby 本体に委ね、糖衣の追加にも追従不要。
    // choose は回答を変えて選択肢数ぶん再実行し、単一の回答で到達できる分岐を全て通す
    // (複数の choose の組合せでしか到達しない行は対象外。語彙の無い game 独自コマンドは no-op stub 化して流し直す)。

    internal enum ScenarioKeyKind
    {
        Speaker,      // say / portrait / stage / exit のキャラ id
        PortraitKey,  // 立ち絵キー (画像キー)
        Image,        // bg / still / image
        Se,
        Bgm,
        Layout,       // stage の構図 id
    }

    // 実行中に Router へ流れたコマンドからキーを拾う購読者 (ハンドラと並んで購読するだけの受動的な記録係)
    [Routes]
    internal sealed partial class ScenarioKeyRecorder
    {
        public readonly HashSet<(ScenarioKeyKind Kind, string Key)> Keys = new();
        public int MaxChoiceOptions { get; private set; }

        public void On(SayCommand cmd)
        {
            Add(ScenarioKeyKind.Speaker, cmd.SpeakerId);            // "" = ナレーションは対象外
            Add(ScenarioKeyKind.PortraitKey, cmd.PortraitKey);      // say の第 3 引数 (同時立ち絵)
        }

        public void On(ChooseCommand cmd) =>
            MaxChoiceOptions = Math.Max(MaxChoiceOptions, cmd.Options?.Length ?? 0);

        public void On(PortraitCommand cmd)
        {
            Add(ScenarioKeyKind.Speaker, cmd.Character);
            Add(ScenarioKeyKind.PortraitKey, cmd.PortraitKey);
        }

        public void On(StageCommand cmd)
        {
            Add(ScenarioKeyKind.Layout, cmd.LayoutId);
            var pairs = cmd.CastPairs ?? Array.Empty<string>();
            for (var i = 0; i + 1 < pairs.Length; i += 2)
                Add(ScenarioKeyKind.Speaker, pairs[i]);
        }

        public void On(ExitCommand cmd) => Add(ScenarioKeyKind.Speaker, cmd.Character);
        public void On(BackgroundCommand cmd) => Add(ScenarioKeyKind.Image, cmd.BackgroundKey);
        public void On(StillCommand cmd) => Add(ScenarioKeyKind.Image, cmd.StillKey);
        public void On(CenterImageCommand cmd) => Add(ScenarioKeyKind.Image, cmd.ImageKey);
        public void On(SeCommand cmd) => Add(ScenarioKeyKind.Se, cmd.SeKey);
        public void On(SeLoopCommand cmd) => Add(ScenarioKeyKind.Se, cmd.SeKey);
        public void On(BgmCommand cmd) => Add(ScenarioKeyKind.Bgm, cmd.BgmKey);   // "" = 停止は Add が弾く

        private void Add(ScenarioKeyKind kind, string? key)
        {
            if (!string.IsNullOrEmpty(key)) Keys.Add((kind, key!));
        }
    }

    internal static class ScenarioKeyValidator
    {
        // 実行の上限。choose の選択肢数がこれを超える分は回さない (通常 2〜4)
        private const int MaxPasses = 8;

        // 1 シナリオで stub 化する未登録コマンドの上限 (= 流し直しの上限)
        private const int MaxUnknownCommands = 32;

        internal sealed class CollectResult
        {
            public HashSet<(ScenarioKeyKind Kind, string Key)> Keys { get; } = new();

            // 完走できなかったパスのエラー (書き間違い等)。null = 全パス完走
            public string? ExecutionError { get; set; }

            // no-op stub に置き換えて読み飛ばした未登録コマンド名 (game 独自コマンドか誤記)
            public HashSet<string> UnknownCommands { get; } = new();

            // 選択肢数が実行上限を超え、回さなかった回答の分岐が残っている場合の選択肢数 (0 = 全回答を実行済み)
            public int UncoveredChoiceOptions { get; set; }
        }

        // 正解データ。null はその種別の検証をスキップ (情報源が無い = 白黒つけられない)
        internal sealed class KnownKeys
        {
            public HashSet<string>? Speakers;
            public HashSet<string>? ImageKeys;   // Resources スプライトの全サフィックス (ローダの root を知らないため後方一致)
            public HashSet<string>? SeKeys;
            public HashSet<string>? BgmKeys;
            public HashSet<string>? Layouts;
        }

        /// <summary>
        /// シナリオを choose の回答を変えながらスタブ実行し、Router に流れた全キーの和集合を返す。
        /// 早送り (NovelResumePoint.End) で実行するため wait 等の実時間は消費しない。
        /// </summary>
        internal static async UniTask<CollectResult> CollectAsync(IScenarioSource source, string scenarioKey)
        {
            var result = new CollectResult();
            var maxOptions = 1;
            for (var answer = 0; answer < maxOptions && answer < MaxPasses; answer++)
            {
                for (var retry = 0; ; retry++)
                {
                    var recorder = new ScenarioKeyRecorder();
                    var errors = new CaptureErrorHandler();
                    var router = new Router();
                    using var subscription = recorder.MapTo(router);
                    using var runner = new NovelScenarioRunner(source, router,
                        new AnswerView(answer), new IdentityTextResolver(), new EmptyCatalog(),
                        errorHandler: errors,
                        preambleSources: new IPreambleSource[]
                        {
                            new PreambleSource(new ResourcesTextAssetLoader()),
                            new BytesPreambleSource(StubPreamble(result.UnknownCommands)),
                        });

                    await runner.PlayAsync(scenarioKey, NovelResumePoint.End, CancellationToken.None);

                    result.Keys.UnionWith(recorder.Keys);
                    maxOptions = Math.Max(maxOptions, recorder.MaxChoiceOptions);

                    // 未登録コマンドで止まったら、その名前を no-op stub として足して同じ回答で流し直す
                    var unknown = ParseUndefinedMethod(errors.Error);
                    if (unknown == null || !result.UnknownCommands.Add(unknown))
                    {
                        result.ExecutionError ??= errors.Error;
                        break;
                    }
                    // 上限に達したら流し直さない。未検証のまま成功扱いにしないようエラーを残す
                    if (retry >= MaxUnknownCommands)
                    {
                        result.ExecutionError ??= errors.Error;
                        break;
                    }
                }
            }
            if (maxOptions > MaxPasses) result.UncoveredChoiceOptions = maxOptions;
            return result;
        }

        // "undefined method location for Object (NoMethodError)" からコマンド名を取り出す
        private static string? ParseUndefinedMethod(string? error)
        {
            if (error == null) return null;
            var match = System.Text.RegularExpressions.Regex.Match(error, @"undefined method '?([A-Za-z_][A-Za-z0-9_]*[?!]?)'? for");
            return match.Success ? match.Groups[1].Value : null;
        }

        // 収集済みの未登録コマンドを nil を返す no-op として定義する preamble
        private static byte[] StubPreamble(IEnumerable<string> names)
        {
            var source = new System.Text.StringBuilder();
            foreach (var name in names)
                source.Append("def ").Append(name).Append("(*args)\n  nil\nend\n");
            if (source.Length == 0) source.Append("nil\n");

            var state = MRubyState.Create();   // コンパイル専用の一時 state (MRubyState は IDisposable ではない)
            using var compiler = MRubyCS.Compiler.MRubyCompiler.Create(state);
            using var compiled = compiler.CompileToBinaryFormat(System.Text.Encoding.UTF8.GetBytes(source.ToString()));
            return compiled.AsSpan().ToArray();
        }

        private sealed class BytesPreambleSource : IPreambleSource
        {
            private readonly byte[] _bytecode;
            public BytesPreambleSource(byte[] bytecode) => _bytecode = bytecode;
            public UniTask<byte[]?> LoadPreambleAsync(CancellationToken ct) => UniTask.FromResult<byte[]?>(_bytecode);
        }

        /// <summary>集めたキーを正解データと突き合わせ、問題数を返す (問題ごとに Debug.LogWarning 済み)。</summary>
        internal static int Report(string path, string? rubySource, CollectResult collected, KnownKeys known)
        {
            if (collected.ExecutionError != null)
                Debug.LogWarning($"[Novel] {path} スタブ実行が完走しませんでした（書き間違い等。以降の行は未検証）:\n{collected.ExecutionError}");
            if (collected.UnknownCommands.Count > 0)
                Debug.LogWarning($"[Novel] {path} 未登録コマンドを読み飛ばした（game 独自コマンドなら正常。誤記でないか確認）: {string.Join(", ", collected.UnknownCommands)}");
            if (collected.UncoveredChoiceOptions > 0)
                Debug.LogWarning($"[Novel] {path} 選択肢が {collected.UncoveredChoiceOptions} 個あり実行上限（{MaxPasses} 回答）を超えたため、{MaxPasses + 1} 個目以降の回答でしか到達しない分岐は未検証");

            var count = 0;
            foreach (var (kind, key) in collected.Keys)
            {
                var (set, label) = kind switch
                {
                    ScenarioKeyKind.Speaker => (known.Speakers, "キャラ id"),
                    ScenarioKeyKind.PortraitKey => (known.ImageKeys, "立ち絵キー"),
                    ScenarioKeyKind.Image => (known.ImageKeys, "画像キー"),
                    ScenarioKeyKind.Se => (known.SeKeys, "SE キー"),
                    ScenarioKeyKind.Bgm => (known.BgmKeys, "BGM キー"),
                    _ => (known.Layouts, "構図"),
                };
                if (set == null || set.Contains(key)) continue;
                count++;
                var line = FindLine(rubySource, key);
                var at = line > 0 ? $"{path}:{line}" : path;
                Debug.LogWarning($"[Novel] {at} 未定義の{label} '{key}'");
            }
            return count;
        }

        public static KnownKeys BuildKnownKeys()
        {
            var known = new KnownKeys { Speakers = ScanSpeakers(), ImageKeys = ScanImageKeySuffixes() };

            // 音と構図は実行時にしか実体がないため、キャプチャがあるときだけ検証する。
            // キャプチャがあっても種別のキーが 0 件なら「列挙未提供 (EnumerateKeys が空)」とみなし
            // null = スキップに倒す (空集合として扱うと全キーが未定義の大量誤警告になる)
            var snapshot = ProjectReferenceCaptureStore.LoadOrLatest();
            if (snapshot != null)
            {
                var se = new HashSet<string>();
                var bgm = new HashSet<string>();
                foreach (var key in snapshot.AudioKeys)
                    (key.Kind == AudioKeyKind.Bgm ? bgm : se).Add(key.Key);
                known.SeKeys = se.Count > 0 ? se : null;
                known.BgmKeys = bgm.Count > 0 ? bgm : null;

                var layouts = new HashSet<string>();
                foreach (var layout in snapshot.Layouts) layouts.Add(layout.Id);
                known.Layouts = layouts.Count > 0 ? layouts : null;

                // コード実装のカタログ (EnumerateEntries) もキャラの情報源に加える (アセットカタログとの和集合)
                if (snapshot.Characters.Count > 0)
                {
                    known.Speakers ??= new HashSet<string>();
                    foreach (var c in snapshot.Characters) known.Speakers.Add(c.Id);
                }
            }
            return known;
        }

        // 警告に行番号を添える best-effort (キー文字列を含む最初の行。実行ベースの収集は行情報を持たないため)
        private static int FindLine(string? rubySource, string key)
        {
            if (string.IsNullOrEmpty(rubySource)) return 0;
            var lines = rubySource!.Split('\n');
            for (var i = 0; i < lines.Length; i++)
                if (lines[i].IndexOf(key, StringComparison.Ordinal) >= 0)
                    return i + 1;
            return 0;
        }

        // 全 ScriptableCharacterCatalog の id の和集合。カタログが 1 つも無ければ null (検証スキップ)
        private static HashSet<string>? ScanSpeakers()
        {
            HashSet<string>? ids = null;
            foreach (var guid in AssetDatabase.FindAssets("t:ScriptableCharacterCatalog"))
            {
                var asset = AssetDatabase.LoadAssetAtPath<ScriptableCharacterCatalog>(AssetDatabase.GUIDToAssetPath(guid));
                if (asset == null) continue;
                ids ??= new HashSet<string>();
                var entries = new SerializedObject(asset).FindProperty("entries");
                for (var i = 0; i < (entries?.arraySize ?? 0); i++)
                {
                    var id = entries!.GetArrayElementAtIndex(i).FindPropertyRelative("speakerId")?.stringValue;
                    if (!string.IsNullOrEmpty(id)) ids.Add(id!);
                }
            }
            return ids;
        }

        // Resources のスプライトキーを「/ 区切りの全サフィックス」で持つ。ローダに root プレフィックスが
        // 設定されていてもシナリオ側のキーが後方一致すれば正とみなす (誤検知を避ける方向に倒す)。
        // スプライトが 1 枚も無ければ null (独自ローダ運用とみなし検証スキップ)。
        // Tests / Editor 配下はプレイヤービルドに含まれず実行時に解決できないため正解に含めない
        private static HashSet<string>? ScanImageKeySuffixes()
        {
            const string marker = "/Resources/";
            HashSet<string>? suffixes = null;
            foreach (var guid in AssetDatabase.FindAssets("t:Sprite"))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var at = path.IndexOf(marker, StringComparison.Ordinal);
                if (at < 0 || path.Contains("/Tests/") || path.Contains("/Editor/")) continue;
                var key = path.Substring(at + marker.Length);
                var dot = key.LastIndexOf('.');
                if (dot >= 0) key = key.Substring(0, dot);

                suffixes ??= new HashSet<string>();
                for (var i = 0; i >= 0; i = key.IndexOf('/', i + 1))
                    suffixes.Add(i == 0 ? key : key.Substring(i + 1));
            }
            return suffixes;
        }

        // choose に固定の回答 (選択肢数を超えたら最後の選択肢) を返すだけの View
        private sealed class AnswerView : INovelView
        {
            private readonly int _answer;
            public AnswerView(int answer) => _answer = answer;

            public UniTask ShowMessageAsync(NovelLine line, CancellationToken ct) => UniTask.CompletedTask;
            public UniTask<int> ShowChoicesAsync(IReadOnlyList<string> options, CancellationToken ct)
                => UniTask.FromResult(Math.Min(_answer, options.Count - 1));
            public void SetMessageWindowVisible(bool visible) { }
            public void ClearMessage() { }
        }

        private sealed class EmptyCatalog : ICharacterCatalog
        {
            public bool TryGet(string speakerId, out CharacterEntry entry)
            {
                entry = default;
                return false;
            }

            public IEnumerable<CharacterKeyInfo> EnumerateEntries() => Array.Empty<CharacterKeyInfo>();
        }

        private sealed class CaptureErrorHandler : INovelErrorHandler
        {
            public string? Error { get; private set; }
            public void OnScenarioFaulted(NovelErrorInfo error) => Error ??= $"{error.Message}\n{error.Detail}";
        }
    }
}
