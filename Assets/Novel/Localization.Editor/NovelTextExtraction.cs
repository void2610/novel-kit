#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Novel.Editor.Localization;
using UnityEditor.Localization;
using UnityEngine.Localization.Tables;

namespace Novel.Localization.Editor
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
    /// 追跡エンジン（層 1・原稿不変）の計画立案。テーブルの NovelTextSourceMetadata（前回抽出時の
    /// ファイル + 出現順）を基準に今回の走査結果を LCS diff し、原文変更をリネーム/分離/新規/消滅に分類する。
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
        public static void BuildDiff(ExtractionPlan plan, StringTableCollection collection)
        {
            // 前回状態: エントリの NovelTextSourceMetadata (出現ごとに 1 つ) → file → (occurrence, text)
            var previousPerFile = new Dictionary<string, List<(int Occurrence, string Text)>>();
            var previousOccurrenceCount = new Dictionary<string, int>(StringComparer.Ordinal);
            foreach (var entry in collection.SharedData.Entries)
            {
                foreach (var meta in entry.Metadata.MetadataEntries.OfType<NovelTextSourceMetadata>())
                {
                    if (!previousPerFile.TryGetValue(meta.SourceFile, out var list))
                        previousPerFile[meta.SourceFile] = list = new List<(int, string)>();
                    list.Add((meta.Occurrence, entry.Key));
                    previousOccurrenceCount[entry.Key] =
                        previousOccurrenceCount.TryGetValue(entry.Key, out var n) ? n + 1 : 1;
                }
            }

            var currentTextSet = new HashSet<string>(plan.AllCurrentTexts, StringComparer.Ordinal);
            var renamedAway = new HashSet<string>(StringComparer.Ordinal);   // リネームで実際に消費される旧原文

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
                    // 分離とリネーム先の既存キー衝突では旧エントリが残るため、出現を失えば deprecated 対象
                    if (!isSplit && !collection.SharedData.Contains(newText)) renamedAway.Add(oldText);
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
            // 漏れて、出所メタデータを失った宙ぶらりんのエントリになる (レビュー指摘)
            foreach (var text in previousOccurrenceCount.Keys)
                if (!currentTextSet.Contains(text) && !renamedAway.Contains(text))
                    plan.Deprecations.Add(text);

            // 未追跡の新出のうち、既存キーに一致するものは採用 (新規起票と区別せず applier の ensure が吸収)
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

    /// <summary>
    /// 計画のテーブル適用。安定 ID (SharedTableData の KeyId) を保ったキーリネームで訳とメタデータを運ぶ。
    /// 消滅キーは削除せず deprecated マークに留める (訳資産は消さない・ADR)。
    /// </summary>
    public static class ExtractionApplier
    {
        public static void Apply(ExtractionPlan plan, StringTableCollection collection)
        {
            var shared = collection.SharedData;

            foreach (var rename in plan.Renames)
            {
                var oldEntry = shared.GetEntry(rename.OldText);
                if (oldEntry == null)
                {
                    // 同一原文の多重変更等でリネーム元が既に消えている稀ケース → 新規扱いに落とす
                    EnsureEntry(shared, rename.NewText);
                    continue;
                }

                if (shared.Contains(rename.NewText))
                {
                    // 変更後の原文が既存エントリと同文 (別行への収斂)。既存エントリの訳は正当なので
                    // コピーも fuzzy 付けもしない。旧エントリは分離なら他所の出現で生き続け、
                    // 非分離なら出所メタデータ再構築で deprecated 判定に落ちる
                    continue;
                }

                if (rename.IsSplit)
                {
                    var target = shared.AddKey(rename.NewText);               // 新規起票 (既存衝突は上で除外済み)
                    if (rename.Kind != RenameKind.Rewritten)
                        CopyValues(collection, oldEntry, target);              // 訳コピー + fuzzy
                    else
                        ArchiveValues(collection, oldEntry, target, rename.OldText);   // 参考退避のみ (未訳で起票)
                    MarkFuzzy(target, rename);
                    continue;
                }

                shared.RenameKey(rename.OldText, rename.NewText);
                var entry = shared.GetEntry(rename.NewText)!;
                if (rename.Kind == RenameKind.Rewritten)
                {
                    // RenameKey 後は entry.Key が新原文になるため、退避の PreviousSource は旧原文を明示して渡す
                    ArchiveValues(collection, entry, entry, rename.OldText);   // 旧訳を退避して未訳化
                    ClearValues(collection, entry);
                }
                MarkFuzzy(entry, rename);
            }

            foreach (var text in plan.Additions) EnsureEntry(shared, text);

            foreach (var text in plan.Deprecations)
            {
                var entry = shared.GetEntry(text);
                if (entry == null) continue;
                if (!entry.Metadata.MetadataEntries.OfType<NovelDeprecatedMetadata>().Any())
                    entry.Metadata.AddMetadata(new NovelDeprecatedMetadata());
            }

            RebuildSourceMetadata(plan, shared);
            Save(collection);
        }

        // 出所メタデータは毎回ゼロから再構築する (冪等・多重出現も出現ごとに 1 つずつ載る)。
        // 現存キーの deprecated マークはここで解除し (復活対応)、逆に「追跡されていたのに現存しない」キーには
        // マークを保証する (計画の消滅リストの取りこぼしに対する適用時の安全網。追跡実績の無い手動エントリは触らない)
        private static void RebuildSourceMetadata(ExtractionPlan plan, SharedTableData shared)
        {
            var currentTextSet = new HashSet<string>(plan.AllCurrentTexts, StringComparer.Ordinal);
            foreach (var entry in shared.Entries)
            {
                var hadTracking = false;
                foreach (var meta in entry.Metadata.MetadataEntries.OfType<NovelTextSourceMetadata>().ToList())
                {
                    hadTracking = true;
                    entry.Metadata.RemoveMetadata(meta);
                }
                if (currentTextSet.Contains(entry.Key))
                {
                    foreach (var meta in entry.Metadata.MetadataEntries.OfType<NovelDeprecatedMetadata>().ToList())
                        entry.Metadata.RemoveMetadata(meta);
                }
                else if (hadTracking &&
                         !entry.Metadata.MetadataEntries.OfType<NovelDeprecatedMetadata>().Any())
                {
                    entry.Metadata.AddMetadata(new NovelDeprecatedMetadata());
                }
            }
            foreach (var (file, texts) in plan.CurrentPerFile)
            {
                for (var i = 0; i < texts.Count; i++)
                {
                    var entry = shared.GetEntry(texts[i]);
                    entry?.Metadata.AddMetadata(new NovelTextSourceMetadata { SourceFile = file, Occurrence = i });
                }
            }
        }

        private static SharedTableData.SharedTableEntry EnsureEntry(SharedTableData shared, string text)
            => shared.GetEntry(text) ?? shared.AddKey(text);

        private static void MarkFuzzy(SharedTableData.SharedTableEntry entry, RenameOp rename)
        {
            foreach (var meta in entry.Metadata.MetadataEntries.OfType<NovelFuzzyMetadata>().ToList())
                entry.Metadata.RemoveMetadata(meta);
            if (rename.Kind == RenameKind.Rewritten) return;   // リライトは fuzzy でなく未訳 (ADR)
            entry.Metadata.AddMetadata(new NovelFuzzyMetadata
            {
                Reason = rename.Kind == RenameKind.TagOnly ? "tag" : "fuzzy",
                PreviousSource = rename.OldText,
            });
        }

        private static void CopyValues(StringTableCollection collection,
            SharedTableData.SharedTableEntry from, SharedTableData.SharedTableEntry into)
        {
            foreach (var table in collection.StringTables)
            {
                var value = table.GetEntry(from.Id)?.Value;
                if (!string.IsNullOrEmpty(value)) table.AddEntry(into.Key, value);
            }
        }

        private static void ClearValues(StringTableCollection collection, SharedTableData.SharedTableEntry entry)
        {
            foreach (var table in collection.StringTables)
                if (table.GetEntry(entry.Id) != null)
                    table.RemoveEntry(entry.Id);
        }

        // previousSource: リライト前の原文。RenameKey 後は from.Key が新原文になっているため呼び出し側が明示する
        private static void ArchiveValues(StringTableCollection collection,
            SharedTableData.SharedTableEntry from, SharedTableData.SharedTableEntry into, string previousSource)
        {
            foreach (var table in collection.StringTables)
            {
                var value = table.GetEntry(from.Id)?.Value;
                if (string.IsNullOrEmpty(value)) continue;
                into.Metadata.AddMetadata(new NovelArchivedTranslationMetadata
                {
                    PreviousSource = previousSource,
                    LocaleCode = table.LocaleIdentifier.Code,
                    Value = value!,
                });
            }
        }

        private static void Save(StringTableCollection collection)
        {
            UnityEditor.EditorUtility.SetDirty(collection.SharedData);
            foreach (var table in collection.StringTables) UnityEditor.EditorUtility.SetDirty(table);
            UnityEditor.AssetDatabase.SaveAssets();
        }
    }
}
