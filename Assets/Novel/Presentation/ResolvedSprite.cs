#nullable enable
using UnityEngine;

namespace Novel.Assets
{
    /// <summary>
    /// 論理キーと、それを <see cref="ISpriteLoader"/> で解決した結果の対。
    /// スプライトを扱うファセットはこの型で受け取る。
    ///
    /// ロードは runtime (<c>NovelCommandHandler</c>) が済ませるため View に解決の裁量はないが、
    /// キーは表示以外の用途で必要になる。ロード失敗と消去の区別、同一キー再表示の no-op 判定、
    /// game 側の状態記録 (セーブからの背景復元、イベント CG の解放) はいずれもキーを要求する。
    /// <see cref="IPortraitView"/> が character を表示側のヒントとして受けているのと同じ位置づけ。
    /// </summary>
    public readonly struct ResolvedSprite
    {
        // struct は default や配列要素でコンストラクタを通らず生まれるため、非 null 契約は取得側で担保する
        private readonly string? _key;

        /// <summary>シナリオが指定した論理キー (消去・未指定は空文字)。</summary>
        public string Key => _key ?? "";

        /// <summary>解決済みスプライト (ロード失敗またはローダー未供給なら null)。</summary>
        public readonly Sprite? Sprite;

        /// <summary>キーが実際にスプライトへ解決できたか。</summary>
        public bool IsLoaded => Sprite != null;

        public ResolvedSprite(string key, Sprite? sprite)
        {
            _key = key;
            Sprite = sprite;
        }

        /// <summary>何も指すもののない値 (消去やローダー未供給の経路で使う)。</summary>
        public static ResolvedSprite None => default;
    }
}
