#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Novel.Editor.Localization
{
    public enum RenameKind { TagOnly, Fuzzy, Rewritten }

    public sealed class RenameOp
    {
        public string OldText = "";
        public string NewText = "";
        public RenameKind Kind;
        public float Similarity;
        public string File = "";
        public bool IsSplit;   // 旧原文が他所で使用中 (または多重出現) → リネームせず新エントリへ分離
    }

    // 追跡抽出の実行計画。ScenarioTextScanner + TrackedTextDiffer の結果をテーブル操作へ翻訳したもの。
    // 適用前にレポートウィンドウで人間が確認する (localization-unity-package ADR)
    public sealed class ExtractionPlan
    {
        public readonly Dictionary<string, List<string>> CurrentPerFile = new();   // file → 原文 (出現順)
        public readonly List<RenameOp> Renames = new();
        public readonly List<string> Additions = new();      // 新規 (未訳エントリ起票)
        public readonly List<string> Deprecations = new();   // どこからも消えた原文 (訳は消さずマークのみ)
        public readonly List<string> Issues = new();         // 補間スキップ等の報告

        public IEnumerable<string> AllCurrentTexts => CurrentPerFile.Values.SelectMany(t => t);
    }

    /// <summary>
    /// 追跡エンジン（層 1・原稿不変）の計画立案。テーブルの出所メタデータ（前回抽出時のファイル + 出現順）を
    /// 基準に今回の走査結果を LCS diff し、原文変更をリネーム/分離/新規/消滅に分類する。
    /// </summary>
    public static class ExtractionPlanner
    {
        // .rb を集めて走査する。対象外: "~" フォルダ (Unity 非インポート)・"Tests"/"Editor" フォルダ配下
        // (テストフィクスチャやツール用スクリプトを製品テーブルへ混入させない)・プロセを含まない preamble。
        // relativeRoot (Assets からの相対) でシナリオ置き場だけに絞れる (空 = Assets 全体)
        public static ExtractionPlan Scan(string assetsPath, string relativeRoot = "")
        {
            var plan = new ExtractionPlan();
            var root = string.IsNullOrEmpty(relativeRoot)
                ? assetsPath
                : Path.Combine(assetsPath, relativeRoot.Replace('\\', '/').TrimStart('/'));
            if (!Directory.Exists(root))
            {
                plan.Issues.Add($"スキャンルートが見つかりません: Assets/{relativeRoot}");
                return plan;
            }

            var files = Directory.GetFiles(root, "*.rb", SearchOption.AllDirectories)
                .Where(p => !p.Replace('\\', '/').Split('/').Any(seg =>
                    seg.EndsWith("~", StringComparison.Ordinal) ||
                    seg.Equals("Tests", StringComparison.Ordinal) ||
                    seg.Equals("Editor", StringComparison.Ordinal)))
                .Where(p => !Path.GetFileNameWithoutExtension(p).Equals("preamble", StringComparison.OrdinalIgnoreCase))
                .OrderBy(p => p, StringComparer.Ordinal)
                .ToList();
            var charaSet = new HashSet<string>();
            var sources = files.ToDictionary(f => f, File.ReadAllText);
            foreach (var source in sources.Values) ScenarioTextScanner.CollectCharaDeclarations(source, charaSet);

            foreach (var file in files)
            {
                var relative = ToRelativePath(assetsPath, file);
                var scan = ScenarioTextScanner.Scan(sources[file], charaSet);
                plan.CurrentPerFile[relative] = scan.Texts.Select(t => t.Text).ToList();
                foreach (var issue in scan.Issues)
                    plan.Issues.Add($"{relative}:{issue.LineNumber} {issue.Reason}");
            }
            return plan;
        }

        // テーブルの追跡メタデータから前回状態を復元し、diff を計画へ落とす
        public static void BuildDiff(ExtractionPlan plan, ITextTableEditor table)
        {
            // 前回状態: 出所メタデータ (出現ごとに 1 つ) → file → (occurrence, text)
            var previousPerFile = new Dictionary<string, List<(int Occurrence, string Text)>>();
            var previousOccurrenceCount = new Dictionary<string, int>(StringComparer.Ordinal);
            foreach (var key in table.Keys)
            {
                foreach (var source in table.GetSources(key))
                {
                    if (!previousPerFile.TryGetValue(source.SourceFile, out var list))
                        previousPerFile[source.SourceFile] = list = new List<(int, string)>();
                    list.Add((source.Occurrence, key));
                    previousOccurrenceCount[key] =
                        previousOccurrenceCount.TryGetValue(key, out var n) ? n + 1 : 1;
                }
            }

            var currentTextSet = new HashSet<string>(plan.AllCurrentTexts, StringComparer.Ordinal);
            var renamedAway = new HashSet<string>(StringComparer.Ordinal);             // リネームで実際に消費される旧原文
            var reservedRenameTargets = new HashSet<string>(StringComparer.Ordinal);   // 本計画内で先着予約済みのリネーム先

            foreach (var file in plan.CurrentPerFile.Keys.Union(previousPerFile.Keys).ToList())
            {
                var previous = previousPerFile.TryGetValue(file, out var prevList)
                    ? prevList.OrderBy(p => p.Occurrence).Select(p => p.Text).ToList()
                    : new List<string>();
                var current = plan.CurrentPerFile.TryGetValue(file, out var currList) ? currList : new List<string>();

                var diff = TrackedTextDiffer.Diff(previous, current);
                foreach (var change in diff.Changes)
                {
                    var oldText = previous[change.PreviousIndex];
                    var newText = current[change.CurrentIndex];
                    if (oldText == newText) continue;
                    // 共有行ルール: 旧原文が他所でまだ使われている / 多重出現なら、エントリをリネームせず
                    // 変更側だけ新エントリへ分離する (他所の occurrence を巻き込まない)
                    var isSplit = currentTextSet.Contains(oldText) ||
                                  previousOccurrenceCount.TryGetValue(oldText, out var n) && n > 1;
                    // 旧エントリが実際に消費される (リネームされる) 場合のみ deprecated 候補から除外する。
                    // 分離・リネーム先の既存キー衝突では旧エントリが残る。さらに複数のリネームが同じ未登録の
                    // NewText を指す場合、Apply でリネームされるのは先着 1 件だけ (後続はターゲット衝突で旧キー
                    // 残留) のため、計画内の予約も追跡して先着だけを除外する (applier の適用順 = 本ループ順)
                    if (!isSplit && !table.ContainsKey(newText) && reservedRenameTargets.Add(newText))
                        renamedAway.Add(oldText);
                    plan.Renames.Add(new RenameOp
                    {
                        OldText = oldText,
                        NewText = newText,
                        Kind = change.Kind switch
                        {
                            TextChangeKind.TagOnly => RenameKind.TagOnly,
                            TextChangeKind.FuzzyCarry => RenameKind.Fuzzy,
                            _ => RenameKind.Rewritten,
                        },
                        Similarity = change.Similarity,
                        File = file,
                        IsSplit = isSplit,
                    });
                }
                foreach (var index in diff.AddedCurrent) plan.Additions.Add(current[index]);
            }

            // 消滅判定は「前回追跡していた全原文」を母集合にする。RemovedPrevious (対にならなかった消滅) だけでは、
            // 全出現が変更された多重出現原文 (全 split → 旧エントリ残留) とリネーム先衝突 (旧エントリ残留) が
            // 漏れて、出所メタデータを失った宙ぶらりんのエントリになる
            foreach (var text in previousOccurrenceCount.Keys)
                if (!currentTextSet.Contains(text) && !renamedAway.Contains(text))
                    plan.Deprecations.Add(text);

            // 未追跡の新出のうち、リネーム先と一致するものは applier のリネームで起票される
            plan.Additions.RemoveAll(t => plan.Renames.Any(r => r.NewText == t));
        }

        private static string ToRelativePath(string assetsPath, string fullPath)
        {
            var normalized = fullPath.Replace('\\', '/');
            var root = assetsPath.Replace('\\', '/').TrimEnd('/');
            return normalized.StartsWith(root, StringComparison.Ordinal)
                ? "Assets" + normalized.Substring(root.Length)
                : normalized;
        }
    }
}
