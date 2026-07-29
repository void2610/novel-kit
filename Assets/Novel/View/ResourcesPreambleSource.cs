#nullable enable
using System.Threading;
using Cysharp.Threading.Tasks;
using Novel.Runtime;

namespace Novel.View
{
    /// <summary>Resources 既定の <see cref="IPreambleSource"/>。実体は <see cref="TextAssetPreambleSource"/> + <see cref="ResourcesTextAssetLoader"/>。</summary>
    public sealed class ResourcesPreambleSource : IPreambleSource
    {
        private readonly TextAssetPreambleSource _inner;

        public ResourcesPreambleSource(string path = "Novel/Preamble") => _inner = new TextAssetPreambleSource(new ResourcesTextAssetLoader(), path);

        public UniTask<byte[]?> LoadPreambleAsync(CancellationToken ct) => _inner.LoadPreambleAsync(ct);
    }
}
