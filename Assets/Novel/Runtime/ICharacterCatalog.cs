#nullable enable
namespace Novel.Runtime
{
    // 話者 id の解決結果。未登録 id は id 文字列をそのまま表示名にフォールバックする
    public readonly struct CharacterEntry
    {
        public string DisplayName { get; }
        public string? DefaultPortraitKey { get; }

        public CharacterEntry(string displayName, string? defaultPortraitKey = null)
        {
            DisplayName = displayName;
            DefaultPortraitKey = defaultPortraitKey;
        }
    }

    // id → 表示名 / 立ち絵 (slot 位置は IPortraitDirector の stage 宣言で決まる。 voice は v1 対象外)
    public interface ICharacterCatalog
    {
        bool TryGet(string speakerId, out CharacterEntry entry);
    }
}
