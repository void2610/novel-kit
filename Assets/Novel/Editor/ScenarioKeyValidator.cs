#nullable enable
using System.Collections.Generic;
using System.Text.RegularExpressions;
using Novel.Runtime;
using UnityEditor;
using UnityEngine;

namespace Novel.Editor
{
    // .rb シナリオが使うキー (キャラ/立ち絵/画像/音/構図) が実在するかの突き合わせ (project-reference ADR)。
    // 正解データ: キャラ = ScriptableCharacterCatalog、画像 = Resources のスプライト、
    // 音/構図 = DI ビルド時キャプチャ (未キャプチャならその項目はスキップ)。
    // 未配線・キー間違いは実行時に無音 no-op で流れる設計のため、ここで編集時に警告する。

    internal enum ScenarioKeyKind
    {
        Speaker,   // chara 宣言 / say・portrait の話者 id
        Portrait,  // portrait の立ち絵キー (画像キー)
        Image,     // bg / still / image
        Se,
        Bgm,
        Layout,    // stage の構図 id
    }

    internal readonly struct ScenarioKeyUsage
    {
        public ScenarioKeyKind Kind { get; }
        public string Key { get; }
        public int Line { get; }

        public ScenarioKeyUsage(ScenarioKeyKind kind, string key, int line)
        {
            Kind = kind;
            Key = key;
            Line = line;
        }
    }

    // Ruby を実行せず正規表現で拾う。変数・式で組み立てたキー (#{...}) は対象外
    internal static class ScenarioKeyScanner
    {
        private const string Q = @"[""']([^""']+)[""']";
        private static readonly Regex Chara = new(@"(?<![\w.])chara[\s(]+:?[""']?(\w+)", RegexOptions.Compiled);
        private static readonly Regex Say = new(@"(?<![\w.])say[\s(]+" + Q + @"\s*,", RegexOptions.Compiled);
        private static readonly Regex Portrait = new(@"(?<![\w.])portrait[\s(]+:?[""']?(\w+)[""']?\s*,\s*" + Q, RegexOptions.Compiled);
        private static readonly Regex Image = new(@"(?<![\w.])(?:bg|still|image)[\s(]+" + Q, RegexOptions.Compiled);
        private static readonly Regex Audio = new(@"(?<![\w.])(se_loop|se|bgm)[\s(]+" + Q, RegexOptions.Compiled);
        private static readonly Regex Stage = new(@"(?<![\w.])stage[\s(]+:?[""']?(\w+)", RegexOptions.Compiled);

        public static List<ScenarioKeyUsage> Scan(string rubySource)
        {
            var result = new List<ScenarioKeyUsage>();
            var lines = rubySource.Split('\n');
            for (var i = 0; i < lines.Length; i++)
            {
                var line = StripComment(lines[i]);
                var no = i + 1;
                foreach (Match m in Chara.Matches(line)) Add(result, ScenarioKeyKind.Speaker, m.Groups[1].Value, no);
                foreach (Match m in Say.Matches(line)) Add(result, ScenarioKeyKind.Speaker, m.Groups[1].Value, no);
                foreach (Match m in Portrait.Matches(line))
                {
                    Add(result, ScenarioKeyKind.Speaker, m.Groups[1].Value, no);
                    Add(result, ScenarioKeyKind.Portrait, m.Groups[2].Value, no);
                }
                foreach (Match m in Image.Matches(line)) Add(result, ScenarioKeyKind.Image, m.Groups[1].Value, no);
                foreach (Match m in Audio.Matches(line))
                    Add(result, m.Groups[1].Value == "bgm" ? ScenarioKeyKind.Bgm : ScenarioKeyKind.Se, m.Groups[2].Value, no);
                foreach (Match m in Stage.Matches(line)) Add(result, ScenarioKeyKind.Layout, m.Groups[1].Value, no);
            }
            return result;
        }

        private static void Add(List<ScenarioKeyUsage> result, ScenarioKeyKind kind, string key, int line)
        {
            // 空キー (bgm "" = 停止等) と実行時に組み立てるキーは検証対象外
            if (string.IsNullOrEmpty(key) || key.Contains("#{")) return;
            result.Add(new ScenarioKeyUsage(kind, key, line));
        }

        // クォート内でない # から行末を落とす
        internal static string StripComment(string line)
        {
            var inSingle = false;
            var inDouble = false;
            for (var i = 0; i < line.Length; i++)
            {
                var c = line[i];
                if (c == '\\') { i++; continue; }
                if (c == '\'' && !inDouble) inSingle = !inSingle;
                else if (c == '"' && !inSingle) inDouble = !inDouble;
                else if (c == '#' && !inSingle && !inDouble) return line.Substring(0, i);
            }
            return line;
        }
    }

    internal static class ScenarioKeyValidator
    {
        // 正解データ。null はその種別の検証をスキップ (情報源が無い = 白黒つけられない)
        internal sealed class KnownKeys
        {
            public HashSet<string>? Speakers;
            public HashSet<string>? ImageKeys;   // Resources スプライトの全サフィックス (ローダの root を知らないため後方一致)
            public HashSet<string>? SeKeys;
            public HashSet<string>? BgmKeys;
            public HashSet<string>? Layouts;
        }

        /// <summary>検出した問題数を返す (問題ごとに Debug.LogWarning 済み)。</summary>
        public static int Validate(string path, string rubySource, KnownKeys known)
        {
            var count = 0;
            foreach (var usage in ScenarioKeyScanner.Scan(rubySource))
            {
                var (set, label) = usage.Kind switch
                {
                    ScenarioKeyKind.Speaker => (known.Speakers, "キャラ id"),
                    ScenarioKeyKind.Portrait => (known.ImageKeys, "立ち絵キー"),
                    ScenarioKeyKind.Image => (known.ImageKeys, "画像キー"),
                    ScenarioKeyKind.Se => (known.SeKeys, "SE キー"),
                    ScenarioKeyKind.Bgm => (known.BgmKeys, "BGM キー"),
                    _ => (known.Layouts, "構図"),
                };
                if (set == null || set.Contains(usage.Key)) continue;
                count++;
                Debug.LogWarning($"[Novel] {path}:{usage.Line} 未定義の{label} '{usage.Key}'");
            }
            return count;
        }

        public static KnownKeys BuildKnownKeys()
        {
            var known = new KnownKeys { Speakers = ScanSpeakers(), ImageKeys = ScanImageKeySuffixes() };

            // 音と構図は実行時にしか実体がないため、キャプチャがあるときだけ検証する
            var snapshot = ProjectReferenceCaptureStore.LoadOrLatest();
            if (snapshot != null)
            {
                known.SeKeys = new HashSet<string>();
                known.BgmKeys = new HashSet<string>();
                foreach (var key in snapshot.AudioKeys)
                    (key.Kind == AudioKeyKind.Bgm ? known.BgmKeys : known.SeKeys).Add(key.Key);
                known.Layouts = new HashSet<string>();
                foreach (var layout in snapshot.Layouts) known.Layouts.Add(layout.Id);
            }
            return known;
        }

        // 全 ScriptableCharacterCatalog の id の和集合。カタログが 1 つも無ければ null (検証スキップ)
        private static HashSet<string>? ScanSpeakers()
        {
            HashSet<string>? ids = null;
            foreach (var guid in AssetDatabase.FindAssets("t:ScriptableCharacterCatalog"))
            {
                var asset = AssetDatabase.LoadAssetAtPath<View.ScriptableCharacterCatalog>(AssetDatabase.GUIDToAssetPath(guid));
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
        // スプライトが 1 枚も無ければ null (独自ローダ運用とみなし検証スキップ)
        private static HashSet<string>? ScanImageKeySuffixes()
        {
            const string marker = "/Resources/";
            HashSet<string>? suffixes = null;
            foreach (var guid in AssetDatabase.FindAssets("t:Sprite"))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var at = path.IndexOf(marker, System.StringComparison.Ordinal);
                if (at < 0 || path.Contains("/Tests/")) continue;
                var key = path.Substring(at + marker.Length);
                var dot = key.LastIndexOf('.');
                if (dot >= 0) key = key.Substring(0, dot);

                suffixes ??= new HashSet<string>();
                for (var i = 0; i >= 0; i = key.IndexOf('/', i + 1))
                    suffixes.Add(i == 0 ? key : key.Substring(i + 1));
            }
            return suffixes;
        }
    }
}
