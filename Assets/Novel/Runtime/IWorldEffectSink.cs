#nullable enable
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;

namespace Novel.Runtime
{
    // 会話の外側（カメラ/画面/gameplay）へ作用するエフェクトのマーカー
    public interface IWorldEffect { }

    // DSL の world_effect が発行する汎用エフェクト搬送体。game の sink が Key/Args を解釈する
    public readonly struct WorldEffect : IWorldEffect
    {
        public string Key { get; }
        public IReadOnlyList<float> Args { get; }

        public WorldEffect(string key, IReadOnlyList<float> args)
        {
            Key = key;
            Args = args;
        }

        public float Arg(int index, float fallback = 0f)
            => index >= 0 && index < Args.Count ? Args[index] : fallback;
    }

    // 世界エフェクトの脱出先（game が任意供給。既定はブリッジ無し）
    public interface IWorldEffectSink
    {
        // 非ブロッキングは即完了タスク、ブロッキングは完了時解決タスクを返す（ハンドラ await で進行が決まる）
        UniTask DispatchAsync(IWorldEffect effect, CancellationToken ct);
    }
}
