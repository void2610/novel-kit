#nullable enable
using System;
using System.Collections.Generic;
using Void2610.CinematicEffect;

namespace Novel.Cinematic
{
    /// <summary>
    /// <c>&lt;key&gt;_exit</c> アセットが無いときの Exit を Enter から導出する。
    /// Enter が <c>Play</c> (撃ちっぱなし = サスティン) したまま <c>Stop</c> していないエフェクトを、
    /// その Play と同じ config で Stop する。Director は Stop の config が null だと既定へリセットしてから
    /// 停止するため、設計者が Play 側に詰めた exit 尺を引き継ぐには同じ config を渡す必要がある。
    /// <c>PlayAndAwait</c> は一回完結で自然に終わるため対象外。
    /// </summary>
    public static class CinematicExitDeriver
    {
        public static CinematicSequence? Derive(CinematicSequenceAsset enter, Func<Type, bool>? isPlaying = null)
        {
            var sustained = new List<(Type Type, CinematicEffectConfig? Config)>();
            foreach (var step in enter.steps)
            {
                if (step.kind == CinematicSequence.StepKind.Delay) continue;
                var type = step.GetEffectSystemType();
                sustained.RemoveAll(s => s.Type == type);
                if (step.kind == CinematicSequence.StepKind.Play)
                    sustained.Add((type, step.GetActiveConfig()));
            }

            CinematicSequence? sequence = null;
            foreach (var (type, config) in sustained)
            {
                // 既に止まっているものへ Stop を撃つと逆再生アニメだけが走るため、動いているものに限る
                if (isPlaying != null && !isPlaying(type)) continue;
                sequence ??= CinematicSequence.Create();
                sequence.Stop(type, config);
            }
            return sequence;
        }
    }
}
