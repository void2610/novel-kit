#nullable enable
namespace Novel.Runtime
{
    /// <summary>
    /// <see cref="ICharacterCatalog"/> が解決できる話者の目録エントリ (project-reference ADR)。
    /// アセットを持たないコード実装のカタログでも、エディタのプロジェクトリファレンスや
    /// シナリオ検証に「使える id の一覧」を提供するための読み口。
    /// </summary>
    public readonly struct CharacterKeyInfo
    {
        public string Id { get; }
        public string DisplayName { get; }
        public string? DefaultPortraitKey { get; }

        public CharacterKeyInfo(string id, string displayName, string? defaultPortraitKey = null)
        {
            Id = id;
            DisplayName = displayName;
            DefaultPortraitKey = defaultPortraitKey;
        }
    }
}
