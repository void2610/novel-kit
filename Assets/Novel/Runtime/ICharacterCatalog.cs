#nullable enable
using System.Collections.Generic;

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

        /// <summary>
        /// この実装が解決できる話者の目録 (project-reference ADR)。エディタのプロジェクトリファレンスと
        /// シナリオ検証が DI ビルド時に読む。id の列挙のみで、重い処理を伴わないこと。
        /// 既定実装は置かない — 実装忘れが「再生してもキャラ情報が出ない」という沈黙の空目録になるため、
        /// 明示実装をコンパイルエラーで要求する。一覧を持てない実装は空を明示的に返す。
        /// </summary>
        IEnumerable<CharacterKeyInfo> EnumerateEntries();
    }
}
