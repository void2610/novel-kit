#nullable enable
using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using Void2610.CinematicEffect;

namespace Novel.Cinematic
{
    /// <summary>CinematicEffectDirector の実行口。MonoBehaviour を立てずにテストできるよう抽象化する。</summary>
    public interface ICinematicRunner
    {
        UniTask RunAsync(CinematicSequence sequence, CancellationToken ct);
        bool IsPlaying(Type effectType);
    }

    public sealed class DirectorCinematicRunner : ICinematicRunner
    {
        private readonly CinematicEffectDirector _director;

        public DirectorCinematicRunner(CinematicEffectDirector director)
        {
            _director = director;
        }

        public UniTask RunAsync(CinematicSequence sequence, CancellationToken ct) => _director.RunAsync(sequence, ct);
        public bool IsPlaying(Type effectType) => _director.IsPlaying(effectType);

        /// <summary>シーンの Director を使い、無ければ生成する (各エフェクトは自己生成するため事前配置は不要)。</summary>
        public static CinematicEffectDirector FindOrCreateDirector()
        {
            var existing = UnityEngine.Object.FindFirstObjectByType<CinematicEffectDirector>(FindObjectsInactive.Include);
            if (existing != null) return existing;
            return new GameObject(nameof(CinematicEffectDirector)).AddComponent<CinematicEffectDirector>();
        }
    }
}
