#nullable enable
#if NOVEL_CINEMATIC_EFFECT
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Threading;
using Cysharp.Threading.Tasks;
using Novel.Cinematic;
using Novel.Runtime;
using Novel.View;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Void2610.CinematicEffect;

namespace Novel.Tests
{
    // Exit 導出の規則・標準 5 種・Validate 連携を固定する。
    // module の「アセットを引いて Director.RunAsync」は配管なので、Director を立ててまでは検証しない
    public sealed class CinematicEffectTests
    {
        private static CinematicSequenceAsset.Step Step(CinematicSequence.StepKind kind, CinematicSequenceAsset.EffectKind effect, bool custom = false)
            => new() { kind = kind, effect = effect, useCustomConfig = custom };

        private static CinematicSequenceAsset Asset(params CinematicSequenceAsset.Step[] steps)
        {
            var asset = ScriptableObject.CreateInstance<CinematicSequenceAsset>();
            asset.steps.AddRange(steps);
            return asset;
        }

        // CinematicSequence.Steps は internal のため、契約検証はリフレクションで覗く
        private static List<(CinematicSequence.StepKind Kind, Type? Type, CinematicEffectConfig? Config)> StepsOf(CinematicSequence seq)
        {
            var steps = (IEnumerable)typeof(CinematicSequence).GetProperty("Steps", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(seq);
            var result = new List<(CinematicSequence.StepKind, Type?, CinematicEffectConfig?)>();
            foreach (var step in steps)
            {
                var t = step.GetType();
                result.Add((
                    (CinematicSequence.StepKind)t.GetProperty("Kind")!.GetValue(step),
                    (Type?)t.GetProperty("EffectType")!.GetValue(step),
                    (CinematicEffectConfig?)t.GetProperty("Config")!.GetValue(step)));
            }
            return result;
        }

        [Test]
        public void Exit導出はPlayしたままのエフェクトを同じconfigでStopする()
        {
            // 回想演出: グレイン/ビネットをサスティン、フラッシュは一回完結、レンズ歪みは自分で止めている
            var enter = Asset(
                Step(CinematicSequence.StepKind.Play, CinematicSequenceAsset.EffectKind.FilmGrain, custom: true),
                Step(CinematicSequence.StepKind.Play, CinematicSequenceAsset.EffectKind.Vignette),
                Step(CinematicSequence.StepKind.PlayAndAwait, CinematicSequenceAsset.EffectKind.ImageFlash, custom: true),
                Step(CinematicSequence.StepKind.Play, CinematicSequenceAsset.EffectKind.LensDistortion, custom: true),
                Step(CinematicSequence.StepKind.Delay, CinematicSequenceAsset.EffectKind.Letterbox),
                Step(CinematicSequence.StepKind.Stop, CinematicSequenceAsset.EffectKind.LensDistortion));

            var derived = CinematicExitDeriver.Derive(enter);

            Assert.That(derived, Is.Not.Null);
            var steps = StepsOf(derived!);
            Assert.That(steps.Select(s => s.Kind), Is.All.EqualTo(CinematicSequence.StepKind.Stop));
            Assert.That(steps.Select(s => s.Type), Is.EqualTo(new[] { typeof(FilmGrainEffect), typeof(VignetteEffect) }));
            Assert.That(steps[0].Config, Is.SameAs(enter.steps[0].filmGrainConfig), "Play 側の custom config を引き継ぐ");
            Assert.That(steps[1].Config, Is.Null, "custom 指定が無ければ既定に任せる");
        }

        [Test]
        public void Exit導出は一発物では何も生まず_止まっているものは省く()
        {
            var oneShot = Asset(
                Step(CinematicSequence.StepKind.Play, CinematicSequenceAsset.EffectKind.LensDistortion),
                Step(CinematicSequence.StepKind.Stop, CinematicSequenceAsset.EffectKind.LensDistortion));
            Assert.That(CinematicExitDeriver.Derive(oneShot), Is.Null);

            var sustain = Asset(Step(CinematicSequence.StepKind.Play, CinematicSequenceAsset.EffectKind.Vignette));
            Assert.That(CinematicExitDeriver.Derive(sustain, _ => false), Is.Null, "再生中でなければ Stop を撃たない");
            Assert.That(CinematicExitDeriver.Derive(sustain, _ => true), Is.Not.Null);
        }

        // Validate Scenarios が opt-in 語彙のキーを (Director 無しで) 収集できることを固定する
        [UnityTest]
        public IEnumerator Validateの拡張がcinematicのキーを収集する() => UniTask.ToCoroutine(async () =>
        {
            var extension = Novel.Editor.ScenarioKeyExtensions.All.SingleOrDefault(e => e.Label == "演出キー");
            Assert.That(extension, Is.Not.Null, "Novel.CinematicEffect.Editor の InitializeOnLoad が登録する");

            var collected = await Novel.Editor.ScenarioKeyValidator.CollectAsync(
                new ScenarioSource(new ResourcesTextAssetLoader()), "test_cinematic");

            Assert.That(collected.ExecutionError, Is.Null);
            Assert.That(collected.UnknownCommands, Is.Empty, "cinematic は stub ではなく拡張の語彙で受ける");
            Assert.That(collected.ExternalKeys[extension!], Is.EquivalentTo(new[] { "vignette", "missing_effect" }));
        });

        [Test]
        public void 標準5種はworld_effectのキーと引数から組める()
        {
            Assert.That(BuiltinTransitionWorldEffectSink.TryBuild(new WorldEffect("unknown", Array.Empty<float>())), Is.Null);
            foreach (var key in new[] { "shake", "flash", "fade_out", "fade_in", "blackout" })
                Assert.That(BuiltinTransitionWorldEffectSink.TryBuild(new WorldEffect(key, Array.Empty<float>())), Is.Not.Null, key);

            var fadeOut = StepsOf(BuiltinTransitionWorldEffectSink.TryBuild(new WorldEffect("fade_out", new[] { 2f }))!);
            Assert.That(fadeOut[0].Kind, Is.EqualTo(CinematicSequence.StepKind.Play));
            Assert.That(((ScreenFadeConfig)fadeOut[0].Config!).EnterDuration, Is.EqualTo(2f), "引数が尺として効く");
        }
    }
}
#endif
