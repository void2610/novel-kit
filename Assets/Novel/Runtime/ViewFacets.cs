#nullable enable
using System.Collections.Generic;
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

        /// <summary>
        /// この実装が解決できる音キーの目録 (project-reference ADR)。エディタのプロジェクトリファレンスが
        /// DI ビルド時に読む。キーの列挙のみで、実アセットのロード等の重い処理を伴わないこと。
        /// 一覧を提供しない実装は既定 (空) のままでよい。
        /// </summary>
        IEnumerable<AudioKeyInfo> EnumerateKeys() => System.Array.Empty<AudioKeyInfo>();
    }
}
