#nullable enable
using System.Threading;
using Cysharp.Threading.Tasks;

namespace Novel.Runtime
{
    // 会話の外側（カメラ/画面/gameplay）へ作用するエフェクトのマーカー
    public interface IWorldEffect { }

    // 世界エフェクトの脱出先（game が任意供給。既定はブリッジ無し）
    public interface IWorldEffectSink
    {
        // 非ブロッキングは即完了タスク、ブロッキングは完了時解決タスクを返す（ハンドラ await で進行が決まる）
        UniTask DispatchAsync(IWorldEffect effect, CancellationToken ct);
    }
}
