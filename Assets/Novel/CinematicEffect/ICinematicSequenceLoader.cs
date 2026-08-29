#nullable enable
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using Void2610.CinematicEffect;

namespace Novel.Cinematic
{
    /// <summary>演出キーから CinematicSequenceAsset を引く。Addressables 等に載せる game はこれを差し替える。</summary>
    public interface ICinematicSequenceLoader
    {
        UniTask<CinematicSequenceAsset?> LoadAsync(string key, CancellationToken ct);
    }

    /// <summary>
    /// 配置規約 <c>Resources/Novel/Effects/&lt;key&gt;.asset</c> の実装。アセットを置くことが登録で、対応表は持たない。
    /// root は規約として固定する (エディタの一覧・検証も同じ場所を走査するため、可変にすると両者がズレる)。
    /// </summary>
    public sealed class ResourcesCinematicSequenceLoader : ICinematicSequenceLoader
    {
        public const string Root = "Novel/Effects/";

        public UniTask<CinematicSequenceAsset?> LoadAsync(string key, CancellationToken ct)
        {
            if (ct.IsCancellationRequested) return UniTask.FromCanceled<CinematicSequenceAsset?>(ct);
            if (string.IsNullOrEmpty(key)) return UniTask.FromResult<CinematicSequenceAsset?>(null);
            var asset = Resources.Load<CinematicSequenceAsset>(Root + key);
            return UniTask.FromResult(asset == null ? null : asset);
        }
    }
}
