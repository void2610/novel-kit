#nullable enable
using System.Threading;
using Cysharp.Threading.Tasks;

namespace Novel.Runtime
{
    // 立ち絵: 場面のレイアウト (= 同時表示人数のテンプレ) と slot index で位置を決め、 1 枚スプライトを差し替える。
    // 「キャラ → slot index」のマップは <see cref="IPortraitDirector"/> 側が管理し、 ここはレイアウト構造のみ知る。
    // 多層合成や演出 (フェード/移動/ハイライト) は実装側の責務 (本 interface はキャンセル可能な await 経路だけ約束する)。
    public interface IPortraitView
    {
        // 場面のレイアウトを切り替える。 既存表示中キャラの移動 / 退場アニメは実装側が差分検出で決める。
        // 例: pair から trio に切替えた場合、 既存の 2 人は新 layout の対応 index に滑らかに移動する。
        UniTask SwitchLayoutAsync(PortraitLayout layout, CancellationToken ct);

        // 現在のレイアウトの指定 slot に立ち絵を表示する (キャラ id は ICharacterCatalog 用のヒント)。
        UniTask ShowAsync(int slotIndex, string character, string portraitKey, CancellationToken ct);

        // 現在のレイアウトの指定 slot を非表示にする (退場アニメは実装側)。
        UniTask HideAsync(int slotIndex, CancellationToken ct);
    }

    // 背景差し替え + イベント CG（一枚絵）
    public interface IBackgroundView
    {
        UniTask ShowAsync(string backgroundKey, CancellationToken ct);
        UniTask ShowStillAsync(string stillKey, CancellationToken ct);
    }

    // 補足画像を画面中央に表示する（立ち絵と同層想定。全画面 CG の IBackgroundView とは別レイヤー）
    public interface ICenterImageView
    {
        UniTask ShowAsync(string imageKey, CancellationToken ct);
        UniTask HideAsync(CancellationToken ct);
    }

    // se/bgm。音量/フェード/ループ/pitch/停止の引数詳細は実装時に確定する
    public interface IAudioChannel
    {
        UniTask PlaySeAsync(string seKey, CancellationToken ct);
        void PlayBgm(string bgmKey);
        void StopBgm();
    }
}
