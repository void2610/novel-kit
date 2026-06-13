#nullable enable
using System.Threading;
using Cysharp.Threading.Tasks;

namespace Novel.Runtime
{
    // 立ち絵: 単一スロットに 1 枚スプライトを差し替える（多層合成/複数配置は v1 無し）
    public interface IPortraitView
    {
        UniTask ShowAsync(string portraitKey, CancellationToken ct);
        UniTask HideAsync(CancellationToken ct);
    }

    // 背景差し替え + イベント CG（一枚絵）
    public interface IBackgroundView
    {
        UniTask ShowAsync(string backgroundKey, CancellationToken ct);
        UniTask ShowStillAsync(string stillKey, CancellationToken ct);
    }

    // se/bgm。音量/フェード/ループ/pitch/停止の引数詳細は実装時に確定する
    public interface IAudioChannel
    {
        UniTask PlaySeAsync(string seKey, CancellationToken ct);
        void PlayBgm(string bgmKey);
        void StopBgm();
    }
}
