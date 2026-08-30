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
    // 標準 5 種と Validate 連携を固定する。
    // module の「アセットを引いて Director.RunAsync」は配管なので、Director を立ててまでは検証しない
    public sealed class CinematicEffectTests
    {
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
            Assert.That(((ScreenFadeConfig)fadeOut[0].Config!).FadeColor, Is.EqualTo(Color.black), "色未指定は黒");
        }

        [Test]
        public void fade系は色名とhexを受け取り不明な色は黒に倒す()
        {
            static Color ColorOf(string key, string color)
                => ((ScreenFadeConfig)StepsOf(BuiltinTransitionWorldEffectSink.TryBuild(new WorldEffect(key, Array.Empty<float>(), color))!)[0].Config!).FadeColor;

            Assert.That(ColorOf("fade_out", "white"), Is.EqualTo(Color.white));
            Assert.That(ColorOf("fade_in", "#ff0000"), Is.EqualTo(Color.red));
            Assert.That(ColorOf("blackout", "white"), Is.EqualTo(Color.white));
            Assert.That(ColorOf("fade_out", "no-such-color"), Is.EqualTo(Color.black));
        }
    }
}
#endif
