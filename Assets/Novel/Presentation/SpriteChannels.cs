#nullable enable
using System.Threading;
using Cysharp.Threading.Tasks;

namespace Novel.Assets
{
    // 実装は View でも Presenter でもよいため "View" ではなく Channel と呼ぶ (IAudioChannel と同じ位置づけ)

    // slot 割り当ても同一立ち絵の出し直しの抑止も IPortraitDirector が持つため、実装は出すだけでよい
    public interface IPortraitChannel
    {
        // 既存キャラの移動/退場アニメは実装側が差分検出で決める
        UniTask SwitchLayoutAsync(PortraitLayout layout, CancellationToken ct);

        /// <summary>
        /// 指定 slot に立ち絵を出す。誰の絵かは渡さない (表示先は slot で決まるため)。
        /// キャラ別に入退場演出を出し分けたい場合は、stage 宣言を写して game 側で slot → character を持つ
        /// </summary>
        UniTask ShowAsync(int slotIndex, ResolvedSprite portrait, CancellationToken ct);

        UniTask HideAsync(int slotIndex, CancellationToken ct);
    }

    /// <summary>背景 (全画面・場面)。</summary>
    public interface IBackgroundChannel
    {
        UniTask ShowAsync(ResolvedSprite background, CancellationToken ct);
    }

    /// <summary>イベント CG (全画面の一枚絵)。背景とはレイヤーも game 側の関心も別なので口を分ける。</summary>
    public interface IStillChannel
    {
        UniTask ShowAsync(ResolvedSprite still, CancellationToken ct);
    }

    /// <summary>補足画像を画面中央に表示する (立ち絵と同層想定。全画面 CG の IStillChannel とは別レイヤー)。</summary>
    public interface ICenterImageChannel
    {
        UniTask ShowAsync(ResolvedSprite image, CancellationToken ct);
        UniTask HideAsync(CancellationToken ct);
    }
}
