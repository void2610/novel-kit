#nullable enable
using System.Threading;
using Cysharp.Threading.Tasks;

namespace Novel.Assets
{
    // 立ち絵。slot 割り当ては IPortraitDirector が持ち、キー→Sprite 解決も runtime 側で済むため View は表示だけを担う
    public interface IPortraitView
    {
        // 既存キャラの移動/退場アニメは実装側が差分検出で決める
        UniTask SwitchLayoutAsync(PortraitLayout layout, CancellationToken ct);

        /// <summary>指定 slot に立ち絵を表示する (character は表示側のヒント)。</summary>
        UniTask ShowAsync(int slotIndex, string character, ResolvedSprite portrait, CancellationToken ct);

        UniTask HideAsync(int slotIndex, CancellationToken ct);
    }

    /// <summary>背景差し替え + イベント CG (一枚絵)。未解決キーを消すか据え置くかは実装側の判断。</summary>
    public interface IBackgroundView
    {
        UniTask ShowAsync(ResolvedSprite background, CancellationToken ct);
        UniTask ShowStillAsync(ResolvedSprite still, CancellationToken ct);
    }

    /// <summary>補足画像を画面中央に表示する (立ち絵と同層想定。全画面 CG の IBackgroundView とは別レイヤー)。</summary>
    public interface ICenterImageView
    {
        UniTask ShowAsync(ResolvedSprite image, CancellationToken ct);
        UniTask HideAsync(CancellationToken ct);
    }
}
