#nullable enable
using System.Threading;
using Cysharp.Threading.Tasks;

namespace Novel.Runtime
{
    /// <summary>
    /// preamble のコンパイル済み .mrb (サブアセット) を読む <see cref="IPreambleSource"/>。
    /// ロード手段は <see cref="ITextAssetLoader"/> で明示する (Resources なら <c>ResourcesTextAssetLoader</c>)。
    /// </summary>
    public sealed class PreambleSource : IPreambleSource
    {
        private readonly ITextAssetLoader _loader;
        private readonly string _path;

        public PreambleSource(ITextAssetLoader loader, string path = "Novel/Preamble")
        {
            _loader = loader;
            _path = path;
        }

        public UniTask<byte[]?> LoadPreambleAsync(CancellationToken ct) => _loader.LoadBytesAsync(_path, ".mrb", ct);
    }
}
