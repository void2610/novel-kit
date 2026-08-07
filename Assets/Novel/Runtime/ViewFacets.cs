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
        /// 既定実装は置かない — 実装忘れが「再生しても音キーが出ない」という沈黙の空目録になるため、
        /// 明示実装をコンパイルエラーで要求する。一覧を持てない実装は空を明示的に返す。
        /// 既に保持している AudioClip 参照を <see cref="AudioKeyInfo.Asset"/> に渡すと (任意)、
        /// プロジェクトリファレンスの試聴がキー体系に依存せず効く。
        /// </summary>
        IEnumerable<AudioKeyInfo> EnumerateKeys();
    }
}
