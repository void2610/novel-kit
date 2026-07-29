#nullable enable
using System.Threading;
using Cysharp.Threading.Tasks;
using Novel.Runtime;

namespace Novel.View
{
    /// <summary>
    /// preamble のコンパイル済み .mrb (サブアセット) を読む <see cref="IPreambleSource"/>。
    /// ロード手段は <see cref="ITextAssetLoader"/> の差し替えで Resources (既定) / Addressables 等を選べる。
    /// </summary>
    public sealed class PreambleSource : IPreambleSource
    {
        private readonly ITextAssetLoader _loader;
        private readonly string _path;

        public PreambleSource(string path = "Novel/Preamble") : this(new ResourcesTextAssetLoader(), path) { }

        public PreambleSource(ITextAssetLoader loader, string path = "Novel/Preamble")
        {
            _loader = loader;
            _path = path;
        }

        public UniTask<byte[]?> LoadPreambleAsync(CancellationToken ct) => _loader.LoadBytesAsync(_path, ".mrb", ct);
    }
}
