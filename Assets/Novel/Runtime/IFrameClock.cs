#nullable enable
using System.Threading;
using Cysharp.Threading.Tasks;

namespace Novel.Runtime
{
    // タイプライタ進行を時間軸で駆動するためのフレーム時計。UnityEngine.Time/PlayerLoop への依存を View 層に閉じ込め、
    // Novel.Runtime の進行エンジンを純 C# に保つ（テストは固定 dt の fake clock でヘッドレス検証できる）。
    public interface IFrameClock
    {
        float DeltaTime { get; }
        UniTask NextFrameAsync(CancellationToken ct);
    }
}
