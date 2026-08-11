#nullable enable
using System.Collections.Generic;

namespace Novel.Editor.Localization
{
    // 追跡メタデータの 1 出現（前回抽出時にどのファイルの何番目に居たか）
    public readonly struct TextSourceRef
    {
        public readonly string SourceFile;
        public readonly int Occurrence;
        public TextSourceRef(string sourceFile, int occurrence)
        {
            SourceFile = sourceFile;
            Occurrence = occurrence;
        }
    }

    /// <summary>
    /// 追従抽出が触る訳テーブルの抽象（localization-unity-package ADR の「バックエンド中立」に対応）。
    /// キーの同一性は実装側の安定 ID が持ち、<see cref="RenameKey"/> はその ID を保ったままキー名だけを
    /// 差し替える契約（＝訳とメタデータが追従する）。
    ///
    /// 計画立案（<see cref="ExtractionPlanner"/>）と破壊的適用（<see cref="ExtractionApplier"/>）を
    /// この抽象越しに書くことで、Unity Localization を導入していない環境でも
    /// リネーム/分離/収斂/退避/deprecated の全経路を EditMode テストで検証できる。
    /// </summary>
    public interface ITextTableEditor
    {
        IReadOnlyList<string> Keys { get; }
        IReadOnlyList<string> LocaleCodes { get; }

        bool ContainsKey(string key);
        void AddKey(string key);
        // 安定 ID を保ったままキー名を差し替える（訳・メタデータは同じエントリに残る）
        void RenameKey(string oldKey, string newKey);

        string? GetValue(string key, string localeCode);
        void SetValue(string key, string localeCode, string value);
        void RemoveValue(string key, string localeCode);

        IReadOnlyList<TextSourceRef> GetSources(string key);
        void ClearSources(string key);
        void AddSource(string key, string sourceFile, int occurrence);

        void SetFuzzy(string key, string reason, string previousSource);
        void ClearFuzzy(string key);

        void SetDeprecated(string key, bool deprecated);

        void AddArchivedTranslation(string key, string previousSource, string localeCode, string value);

        // 変更の確定（アセットの保存など）。純ロジックのテストでは no-op
        void Save();
    }
}
