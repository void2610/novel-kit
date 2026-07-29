#nullable enable
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using Novel.Assets;
using UnityEngine;
using UnityEngine.ResourceManagement.AsyncOperations;

namespace Novel.Addressables
{
    /// <summary>
    /// <see cref="ISpriteLoader"/> の Addressables 実装。キーはアドレス (<c>root</c> を前置可)。
    /// 表示中もスプライト参照が生きている必要があるためハンドルを保持し、<see cref="ReleaseAll"/> でまとめて解放する。
    /// キー未登録・ロード失敗は Resources 実装と同じ null に落とし、原因は警告ログに残す。
    /// </summary>
    public sealed class AddressablesSpriteLoader : ISpriteLoader
    {
        private readonly string _root;
        private readonly Dictionary<string, AsyncOperationHandle<Sprite>> _handles = new();

        public AddressablesSpriteLoader(string root = "")
        {
            _root = root;
        }

        public async UniTask<Sprite?> LoadAsync(string key, CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
            if (string.IsNullOrEmpty(key)) return null;

            // 同一キーの並行呼び出しでハンドルが二重生成・上書きされて解放漏れになるため、
            // ロード開始時点で辞書へ登録し、以降の呼び出しは同じハンドルを await する
            if (!_handles.TryGetValue(key, out var handle))
            {
                handle = UnityEngine.AddressableAssets.Addressables.LoadAssetAsync<Sprite>(_root + key);
                _handles[key] = handle;
            }

            try
            {
                return await handle.ToUniTask(cancellationToken: ct);
            }
            catch (System.OperationCanceledException)
            {
                // キャンセルはロード自体の失敗ではないため、ハンドルは残して後続の呼び出しに再利用させる
                throw;
            }
            catch (System.Exception e) when (e is not System.OutOfMemoryException)
            {
                // 失敗したハンドルはキャッシュから外し、次回の再試行を妨げない
                if (_handles.Remove(key, out var failed))
                    UnityEngine.AddressableAssets.Addressables.Release(failed);
                Debug.LogWarning($"[novel-kit] Addressables スプライトのロード失敗 key='{_root}{key}': {e.GetType().Name}: {e.Message}");
                return null;
            }
        }

        public void ReleaseAll()
        {
            foreach (var handle in _handles.Values)
                UnityEngine.AddressableAssets.Addressables.Release(handle);
            _handles.Clear();
        }
    }
}
