#nullable enable
using System;
using System.Collections.Generic;
using UnityEngine.Localization.Metadata;

namespace Novel.Localization
{
    /// <summary>
    /// 追跡エンジンの出所メタデータ（localization-unity-package ADR「層 1」）。
    /// 前回抽出時の {ファイル, 出現順} を Shared エントリに持たせ、次回抽出の LCS diff の基準にする
    /// （独立サイドカーは持たず、追跡状態はテーブルに同居させる）。
    /// </summary>
    [Metadata(AllowedTypes = MetadataType.SharedTableEntry)]
    [Serializable]
    public class NovelTextSourceMetadata : IMetadata
    {
        public string SourceFile = "";   // Assets からの相対パス
        public int Occurrence;           // ファイル内の出現順 (0 始まり)
    }

    /// <summary>
    /// fuzzy（要再確認）マーク。誤字修正等で原文が変わり、旧訳を保持したまま持ち越したエントリに付く。
    /// ランタイムは保持した訳をそのまま表示する（fuzzy の意味論・ADR）。翻訳者が確認したら外す。
    /// </summary>
    [Metadata(AllowedTypes = MetadataType.SharedTableEntry)]
    [Serializable]
    public class NovelFuzzyMetadata : IMetadata
    {
        public string Reason = "";           // "fuzzy"（高類似）/ "tag"（タグ移植要）
        public string PreviousSource = "";   // 変更前の原文（確認時の突き合わせ用）
    }

    /// <summary>
    /// 消滅マーク。原文がどのシナリオにも見つからなくなったエントリに付く（訳資産は削除しない・ADR）。
    /// </summary>
    [Metadata(AllowedTypes = MetadataType.SharedTableEntry)]
    [Serializable]
    public class NovelDeprecatedMetadata : IMetadata
    {
        public string LastSeenFile = "";
    }

    /// <summary>
    /// リライト時の参考退避（ADR「低類似 → 旧訳をメタデータへ参考退避し未訳化」）。
    /// stale 訳を表示しないためロケール別テーブルからは値を消し、翻訳者の参考としてここに残す。
    /// </summary>
    [Metadata(AllowedTypes = MetadataType.SharedTableEntry)]
    [Serializable]
    public class NovelArchivedTranslationMetadata : IMetadata
    {
        public string PreviousSource = "";     // リライト前の原文
        public string LocaleCode = "";
        public string Value = "";              // 退避した旧訳
    }

    /// <summary>
    /// dev 抽出漏れ収集（ADR「静的抽出の補完」）。糖衣の間接呼びや動的組み立てで静的走査から
    /// 漏れた原文を、実プレイ中のテーブルミスから回収する。game が dev ビルドで
    /// <c>resolver.TextMissed += MissingTextCollector.Record;</c> と配線し、
    /// エディタメニュー（Novel/Localization/Report Missing Texts）で一覧を確認する。
    /// </summary>
    public static class MissingTextCollector
    {
        private static readonly HashSet<string> Missed = new();

        public static void Record(string raw)
        {
            lock (Missed) Missed.Add(raw);
        }

        public static IReadOnlyList<string> Snapshot()
        {
            lock (Missed)
            {
                var list = new List<string>(Missed);
                list.Sort(StringComparer.Ordinal);
                return list;
            }
        }

        public static void Clear()
        {
            lock (Missed) Missed.Clear();
        }
    }
}
