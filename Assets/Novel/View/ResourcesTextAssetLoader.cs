#nullable enable
using System.Threading;
using Cysharp.Threading.Tasks;
using Novel.Runtime;
using UnityEngine;

namespace Novel.View
{
    /// <summary><see cref="ITextAssetLoader"/> の Resources 既定実装。キーは Resources 相対パス (拡張子なし)。</summary>
    public sealed class ResourcesTextAssetLoader : ITextAssetLoader
    {
        public UniTask<byte[]?> LoadBytesAsync(string key, string subAssetSuffix, CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
            foreach (var a in Resources.LoadAll<TextAsset>(key))
                if (a.name.EndsWith(subAssetSuffix, System.StringComparison.Ordinal))
                    return UniTask.FromResult<byte[]?>(a.bytes);
            return UniTask.FromResult<byte[]?>(null);
        }

        public UniTask<string?> LoadTextAsync(string key, CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
            var asset = Resources.Load<TextAsset>(key);
            return UniTask.FromResult(asset != null ? asset.text : null);
        }
    }
}
