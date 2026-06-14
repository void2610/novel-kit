#nullable enable
using System.Threading;
using Cysharp.Threading.Tasks;
using Novel.Runtime;
using UnityEngine;

namespace Novel.View
{
    // IFrameClock の Unity 実装。Time.deltaTime と PlayerLoop の Update yield を Runtime の進行エンジンへ供給する。
    public sealed class UnityFrameClock : IFrameClock
    {
        public float DeltaTime => Time.deltaTime;

        public UniTask NextFrameAsync(CancellationToken ct) => UniTask.Yield(PlayerLoopTiming.Update, ct);
    }
}
