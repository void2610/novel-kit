#nullable enable
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace Novel.Assets
{
    /// <summary>
    /// 論理キーからスプライトを解決するロード戦略の抽象。
    /// 立ち絵・背景・イベント CG を表示する View 実装 (game 所有) が使う。
    ///
    /// テキストと違い Sprite は表示中ずっと参照が生き続ける必要があるため、
    /// <c>ITextAssetLoader</c> のような「中身を抽出して即解放」はできない。
    /// ロード済みハンドルの寿命は実装が持ち、区切り (シナリオ終了など) で <see cref="ReleaseAll"/> を呼んで解放する。
    /// </summary>
    public interface ISpriteLoader
    {
        /// <summary>
        /// キーが指すスプライトを返す (無ければ null)。同一キーの再ロードはキャッシュを返してよい。
        /// spriteMode=Multiple のアセットの扱いは実装依存 (Resources 実装は先頭スライスを返すが順序は保証されない。
        /// Addressables 実装はスライス単位のアドレス指定が要る場合がある) なので、単一スプライトでの利用を推奨する。
        /// </summary>
        UniTask<Sprite?> LoadAsync(string key, CancellationToken ct);

        /// <summary>
        /// 保持しているスプライトを全て解放する。解放後のスプライト参照は無効になる。
        /// ロード中 (await 中) のものは対象外で、完了後に保持され次回の解放対象になる。
        /// </summary>
        void ReleaseAll();
    }
}
