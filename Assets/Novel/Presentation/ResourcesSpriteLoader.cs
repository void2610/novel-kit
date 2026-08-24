#nullable enable
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace Novel.Assets
{
    /// <summary>
    /// <see cref="ISpriteLoader"/> の Resources 実装。キーは Resources 相対パス (拡張子なし)。
    /// <c>root</c> を渡すとキーの前に付与する (例: "Novel/" + "Melia")。
    /// </summary>
    public sealed class ResourcesSpriteLoader : ISpriteLoader, ISpriteKeyPrefix
    {
        private readonly string _root;

        public ResourcesSpriteLoader(string root = "")
        {
            _root = root;
        }

        public string KeyPrefix => _root;

        public UniTask<Sprite?> LoadAsync(string key, CancellationToken ct)
        {
            if (ct.IsCancellationRequested) return UniTask.FromCanceled<Sprite?>(ct);
            if (string.IsNullOrEmpty(key)) return UniTask.FromResult<Sprite?>(null);

            // spriteMode=Multiple のとき Load<Sprite> は null になるため LoadAll で先頭を使う
            var sprites = Resources.LoadAll<Sprite>(_root + key);
            return UniTask.FromResult(sprites.Length > 0 ? sprites[0] : null);
        }

        // Resources は個別解放の手段が無く UnloadUnusedAssets は全体に効く重い操作のため、ここでは何もしない
        public void ReleaseAll() { }
    }
}
