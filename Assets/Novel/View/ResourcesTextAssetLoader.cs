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
            // Addressables 版 (async メソッド) と対称にするため、同期 throw ではなく canceled UniTask で返す
            if (ct.IsCancellationRequested) return UniTask.FromCanceled<byte[]?>(ct);
            foreach (var a in Resources.LoadAll<TextAsset>(key))
                if (a.name.EndsWith(subAssetSuffix, System.StringComparison.Ordinal))
                    return UniTask.FromResult<byte[]?>(a.bytes);
            return UniTask.FromResult<byte[]?>(null);
        }

        public UniTask<string?> LoadTextAsync(string key, CancellationToken ct)
        {
            if (ct.IsCancellationRequested) return UniTask.FromCanceled<string?>(ct);
            return UniTask.FromResult(LoadText(key));
        }

        /// <summary>
        /// テキストを同期で読む (無ければ null)。
        /// PlayerLoop が回っていない Awake / DI の Configure 中は <c>async UniTask</c> の完了を待てないため、
        /// そうした場面ではこちらを使う (UniTask を挟まないので状態機械の再開待ちが発生しない)。
        /// </summary>
        public string? LoadText(string key)
        {
            var asset = Resources.Load<TextAsset>(key);
            return asset != null ? asset.text : null;
        }
    }
}
