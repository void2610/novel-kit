#nullable enable
using System.Threading;
using Cysharp.Threading.Tasks;

namespace Novel.Runtime
{
    // ライブラリ同梱 preamble.rb のコンパイル済みバイトコードを供給する。
    // Resources/サブアセット抽出など Unity 依存の具体実装は Runtime 外（View 層等）に置く
    public interface IPreambleSource
    {
        UniTask<byte[]?> LoadPreambleAsync(CancellationToken ct);
    }
}
