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
    /// <see cref="Snapshot"/> で一覧を取得する（エディタメニュー Novel/Localization/Report Missing Texts は
    /// 抽出ツール群の後続 PR で提供予定）。呼び出しは Unity メインスレッド前提（resolver の Resolve と同じ）。
    /// </summary>
    public static class MissingTextCollector
    {
        private static readonly HashSet<string> Missed = new();

#if UNITY_EDITOR
        // Play Mode 終了時の domain reload で static は消えるため、エディタでは SessionState
        // (エディタセッション中は生存) へ退避する。区切りはテキストに現れない unit separator
        // (万一含まれても復元が壊れないよう Record で除去する)。書き込みはミスごとの write-through ではなく
        // delayCall でフレーム単位にまとめる (通しプレイで新規ミスごとに全集合を再シリアライズしない)
        private const string SessionKey = "Novel.Localization.MissingTexts";
        private const char SeparatorChar = '\u001f';
        private const string Separator = "\u001f";
        private static bool _flushScheduled;

        // domain reload 後、最初のメンバアクセスより前に復元される (型初期化子の実行順は CLR が保証)。
        // ビルドでは in-memory のみ
        static MissingTextCollector()
        {
            var stored = UnityEditor.SessionState.GetString(SessionKey, "");
            if (stored.Length == 0) return;
            foreach (var text in stored.Split(SeparatorChar))
                if (text.Length > 0) Missed.Add(text);
        }
#endif

        public static void Record(string raw)
        {
#if UNITY_EDITOR
            if (raw.IndexOf(SeparatorChar) >= 0) raw = raw.Replace(Separator, "");
            if (!Missed.Add(raw)) return;
            ScheduleFlush();
#else
            Missed.Add(raw);
#endif
        }

        public static IReadOnlyList<string> Snapshot()
        {
            var list = new List<string>(Missed);
            list.Sort(StringComparer.Ordinal);
            return list;
        }

        public static void Clear()
        {
            Missed.Clear();
#if UNITY_EDITOR
            UnityEditor.SessionState.EraseString(SessionKey);
#endif
        }

#if UNITY_EDITOR
        private static void ScheduleFlush()
        {
            if (_flushScheduled) return;
            _flushScheduled = true;
            UnityEditor.EditorApplication.delayCall += () =>
            {
                _flushScheduled = false;
                UnityEditor.SessionState.SetString(SessionKey, string.Join(Separator, Missed));
            };
        }
#endif
    }
}
