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
        private static bool _restored;

#if UNITY_EDITOR
        // Play Mode 終了時の domain reload で static は消えるため、エディタでは SessionState
        // (エディタセッション中は生存) へ write-through する。区切りはテキストに現れない unit separator
        private const string SessionKey = "Novel.Localization.MissingTexts";
        private const char Separator = '\u001f';
#endif

        public static void Record(string raw)
        {
            lock (Missed)
            {
                EnsureRestored();
                if (!Missed.Add(raw)) return;
#if UNITY_EDITOR
                UnityEditor.SessionState.SetString(SessionKey, string.Join(Separator.ToString(), Missed));
#endif
            }
        }

        public static IReadOnlyList<string> Snapshot()
        {
            lock (Missed)
            {
                EnsureRestored();
                var list = new List<string>(Missed);
                list.Sort(StringComparer.Ordinal);
                return list;
            }
        }

        public static void Clear()
        {
            lock (Missed)
            {
                _restored = true;   // 復元をスキップ (消した直後に旧内容が蘇らないように)
                Missed.Clear();
#if UNITY_EDITOR
                UnityEditor.SessionState.EraseString(SessionKey);
#endif
            }
        }

        // domain reload 後の初回アクセスで SessionState から復元する (ビルドでは in-memory のみ)
        private static void EnsureRestored()
        {
            if (_restored) return;
            _restored = true;
#if UNITY_EDITOR
            var stored = UnityEditor.SessionState.GetString(SessionKey, "");
            if (stored.Length == 0) return;
            foreach (var text in stored.Split(Separator))
                if (text.Length > 0) Missed.Add(text);
#endif
        }
    }
}
