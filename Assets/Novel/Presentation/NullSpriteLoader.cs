#nullable enable
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace Novel.Assets
{
    /// <summary><see cref="ISpriteLoader"/> の no-op 既定 (未配線時のフォールバック)。常に null を返す。</summary>
    public sealed class NullSpriteLoader : ISpriteLoader
    {
        public UniTask<Sprite?> LoadAsync(string key, CancellationToken ct) => UniTask.FromResult<Sprite?>(null);
        public void ReleaseAll() { }
    }
}
