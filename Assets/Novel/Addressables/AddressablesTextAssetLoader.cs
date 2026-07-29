#nullable enable
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using Novel.Runtime;
using UnityEngine;
using UnityEngine.AddressableAssets;

namespace Novel.Addressables
{
    /// <summary>
    /// <see cref="ITextAssetLoader"/> の Addressables 実装。キーはアドレス。
    /// 中身 (bytes/text) を抽出して返す契約のため、ロード完了後に即ハンドルを解放する。
    /// キー未登録・ロード失敗は Resources 実装の「見つからない」と同じ null に落とすが、
    /// タイポ・カタログ未初期化・ビルド漏れを診断できるよう警告ログに原因を残す。
    /// </summary>
    public sealed class AddressablesTextAssetLoader : ITextAssetLoader
    {
        public async UniTask<byte[]?> LoadBytesAsync(string key, string subAssetSuffix, CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
            // IList<T> 指定でメイン + サブアセットをまとめて取得する (Addressables のサブオブジェクトロード)
            var handle = UnityEngine.AddressableAssets.Addressables.LoadAssetAsync<IList<TextAsset>>(key);
            try
            {
                var assets = await handle.ToUniTask(cancellationToken: ct);
                foreach (var asset in assets)
                    if (asset.name.EndsWith(subAssetSuffix, System.StringComparison.Ordinal))
                        return asset.bytes;
                return null;
            }
            catch (System.OperationCanceledException)
            {
                throw;
            }
            catch (System.Exception e) when (e is not System.OutOfMemoryException)
            {
                Debug.LogWarning($"[novel-kit] Addressables ロード失敗 key='{key}': {e.GetType().Name}: {e.Message}");
                return null;
            }
            finally
            {
                UnityEngine.AddressableAssets.Addressables.Release(handle);
            }
        }

        public async UniTask<string?> LoadTextAsync(string key, CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
            var handle = UnityEngine.AddressableAssets.Addressables.LoadAssetAsync<TextAsset>(key);
            try
            {
                var asset = await handle.ToUniTask(cancellationToken: ct);
                return asset != null ? asset.text : null;
            }
            catch (System.OperationCanceledException)
            {
                throw;
            }
            catch (System.Exception e) when (e is not System.OutOfMemoryException)
            {
                Debug.LogWarning($"[novel-kit] Addressables ロード失敗 key='{key}': {e.GetType().Name}: {e.Message}");
                return null;
            }
            finally
            {
                UnityEngine.AddressableAssets.Addressables.Release(handle);
            }
        }
    }
}
