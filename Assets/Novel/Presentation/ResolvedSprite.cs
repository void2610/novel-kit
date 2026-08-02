#nullable enable
using UnityEngine;

namespace Novel.Assets
{
    /// <summary>
    /// 論理キーと、それを <see cref="ISpriteLoader"/> で解決した結果の対。
    /// スプライトを扱うファセットはこの型で受け取る。
    ///
    /// ロードは runtime (<c>NovelCommandHandler</c>) が済ませるため実装に解決の裁量はないが、
    /// キーは表示以外の用途で要る。ロード失敗と消去の区別に加え、game が novel-kit の外へ拡張するとき
    /// (CG 回収の記録、シナリオ外での背景の維持) の手掛かりになる。
    /// novel-kit 自身はシナリオ再生の外を関知しないが、game が拡張できる余地は残す
    /// </summary>
    public readonly struct ResolvedSprite
    {
        // struct は default や配列要素でコンストラクタを通らず生まれるため、非 null 契約は取得側で担保する
        private readonly string? _key;

        /// <summary>シナリオが指定した論理キー (消去・未指定は空文字)。</summary>
        public string Key => _key ?? "";

        /// <summary>解決済みスプライト (ロード失敗またはローダー未供給なら null)。</summary>
        public Sprite? Sprite { get; }

        /// <summary>キーが実際にスプライトへ解決できたか (消去とロード失敗はどちらも false)。</summary>
        public bool IsLoaded => Sprite != null;

        /// <summary>キーが空 = 消去指示か。<see cref="IsLoaded"/> が false のときロード失敗と区別するのに使う。</summary>
        public bool IsCleared => Key.Length == 0;

        public ResolvedSprite(string key, Sprite? sprite)
        {
            _key = key;
            Sprite = sprite;
        }

        /// <summary>何も指すもののない値 (消去やローダー未供給の経路で使う)。</summary>
        public static ResolvedSprite None => default;
    }
}
