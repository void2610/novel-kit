#nullable enable
using System.Threading;
using Cysharp.Threading.Tasks;

namespace Novel.Runtime
{
    // se/bgm。音量/フェード/ループ/pitch/停止の引数詳細は実装時に確定する
    // (スプライトを扱う立ち絵/背景/中央画像のファセットは Novel.Assets 側にある)
    public interface IAudioChannel
    {
        UniTask PlaySeAsync(string seKey, CancellationToken ct);
        UniTask PlaySeLoopAsync(string seKey, float interval, int count, CancellationToken ct);
        void PlayBgm(string bgmKey);
        void StopBgm();
    }
}
