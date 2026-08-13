#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;

namespace Novel.Editor.Localization
{
    /// <summary>
    /// 計画のテーブル適用。安定 ID を保ったキーリネームで訳とメタデータを運ぶ
    /// （消滅キーは削除せず deprecated マークに留める・訳資産は消さない。localization-unity-package ADR）。
    /// <see cref="ITextTableEditor"/> 越しに書くことでバックエンド非依存かつテスト可能。
    /// </summary>
    public static class ExtractionApplier
    {
        public static void Apply(ExtractionPlan plan, ITextTableEditor table)
        {
            foreach (var rename in plan.Renames)
            {
                if (!table.ContainsKey(rename.OldText))
                {
                    // 同一原文の多重変更等でリネーム元が既に消えている稀ケース → 新規扱いに落とす
                    EnsureKey(table, rename.NewText);
                    continue;
                }

                if (table.ContainsKey(rename.NewText))
                {
                    // 変更後の原文が既存エントリと同文 (別行への収斂)。既存エントリの訳は正当なので
                    // コピーも fuzzy 付けもしない。旧エントリは分離なら他所の出現で生き続け、
                    // 非分離なら出所メタデータ再構築で deprecated 判定に落ちる
                    continue;
                }

                if (rename.IsSplit)
                {
                    table.AddKey(rename.NewText);                 // 新規起票 (既存衝突は上で除外済み)
                    if (rename.Kind != RenameKind.Rewritten)
                        CopyValues(table, rename.OldText, rename.NewText);                    // 訳コピー + fuzzy
                    else
                        ArchiveValues(table, rename.OldText, rename.NewText, rename.OldText); // 参考退避のみ (未訳で起票)
                    MarkFuzzy(table, rename.NewText, rename);
                    continue;
                }

                table.RenameKey(rename.OldText, rename.NewText);
                if (rename.Kind == RenameKind.Rewritten)
                {
                    // リネーム後はキーが新原文になるため、退避の PreviousSource は旧原文を明示して渡す
                    ArchiveValues(table, rename.NewText, rename.NewText, rename.OldText);   // 旧訳を退避して
                    ClearValues(table, rename.NewText);                                      // 未訳化 (stale 訳を出さない)
                }
                MarkFuzzy(table, rename.NewText, rename);
            }

            foreach (var text in plan.Additions) EnsureKey(table, text);

            foreach (var text in plan.Deprecations)
                if (table.ContainsKey(text))
                    table.SetDeprecated(text, true);

            RebuildSourceMetadata(plan, table);
            table.Save();
        }

        // 出所メタデータは毎回ゼロから再構築する (冪等・多重出現も出現ごとに 1 つずつ載る)。
        // 現存キーの deprecated マークはここで解除し (復活対応)、逆に「追跡されていたのに現存しない」キーには
        // マークを保証する (計画の消滅リストの取りこぼしに対する適用時の安全網。追跡実績の無い手動エントリは触らない)
        private static void RebuildSourceMetadata(ExtractionPlan plan, ITextTableEditor table)
        {
            var currentTextSet = new HashSet<string>(plan.AllCurrentTexts, StringComparer.Ordinal);
            foreach (var key in table.Keys.ToList())
            {
                var hadTracking = table.GetSources(key).Count > 0;
                table.ClearSources(key);
                if (currentTextSet.Contains(key)) table.SetDeprecated(key, false);
                else if (hadTracking) table.SetDeprecated(key, true);
            }
            foreach (var pair in plan.CurrentPerFile)
            {
                for (var i = 0; i < pair.Value.Count; i++)
                {
                    var text = pair.Value[i];
                    if (table.ContainsKey(text)) table.AddSource(text, pair.Key, i);
                }
            }
        }

        private static void EnsureKey(ITextTableEditor table, string text)
        {
            if (!table.ContainsKey(text)) table.AddKey(text);
        }

        private static void MarkFuzzy(ITextTableEditor table, string key, RenameOp rename)
        {
            table.ClearFuzzy(key);
            if (rename.Kind == RenameKind.Rewritten) return;   // リライトは fuzzy でなく未訳 (ADR)
            table.SetFuzzy(key, rename.Kind == RenameKind.TagOnly ? "tag" : "fuzzy", rename.OldText);
        }

        private static void CopyValues(ITextTableEditor table, string fromKey, string intoKey)
        {
            foreach (var locale in table.LocaleCodes)
            {
                var value = table.GetValue(fromKey, locale);
                if (!string.IsNullOrEmpty(value)) table.SetValue(intoKey, locale, value!);
            }
        }

        private static void ClearValues(ITextTableEditor table, string key)
        {
            foreach (var locale in table.LocaleCodes) table.RemoveValue(key, locale);
        }

        // previousSource: リライト前の原文。リネーム後は fromKey が新原文になっているため呼び出し側が明示する
        private static void ArchiveValues(ITextTableEditor table, string fromKey, string intoKey, string previousSource)
        {
            foreach (var locale in table.LocaleCodes)
            {
                var value = table.GetValue(fromKey, locale);
                if (string.IsNullOrEmpty(value)) continue;
                table.AddArchivedTranslation(intoKey, previousSource, locale, value!);
            }
        }
    }
}
