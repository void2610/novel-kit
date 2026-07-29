#nullable enable
using System.Threading;
using Cysharp.Threading.Tasks;
using Novel.Runtime;

namespace Novel.View
{
    /// <summary>
    /// <see cref="ITextAssetLoader"/> 経由で preamble のコンパイル済み .mrb (サブアセット) を読む
    /// <see cref="IPreambleSource"/>。ロード手段はローダー差し替えで Resources / Addressables 等を選べる。
    /// </summary>
    public sealed class TextAssetPreambleSource : IPreambleSource
    {
        private readonly ITextAssetLoader _loader;
        private readonly string _path;

        public TextAssetPreambleSource(ITextAssetLoader loader, string path = "Novel/Preamble")
        {
            _loader = loader;
            _path = path;
        }

        public UniTask<byte[]?> LoadPreambleAsync(CancellationToken ct) => _loader.LoadBytesAsync(_path, ".mrb", ct);
    }
}
