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
using VitalRouter;
using Void2610.CinematicEffect;

namespace Novel.Tests
{
    // 配置規約 + cinematic 語彙 + Exit 導出の契約を固定する (Director の MonoBehaviour は立てず ICinematicRunner を偽装)
    public sealed class CinematicEffectTests
    {
        private sealed class RecordingRunner : ICinematicRunner
        {
            public readonly List<CinematicSequence> Runs = new();
            public readonly HashSet<Type> Playing = new();
            public UniTask RunAsync(CinematicSequence sequence, CancellationToken ct) { Runs.Add(sequence); return UniTask.CompletedTask; }
            public bool IsPlaying(Type effectType) => Playing.Contains(effectType);
        }

        private sealed class DictLoader : ICinematicSequenceLoader
        {
            public readonly Dictionary<string, CinematicSequenceAsset> Assets = new();
            public UniTask<CinematicSequenceAsset?> LoadAsync(string key, CancellationToken ct)
                => UniTask.FromResult(Assets.TryGetValue(key, out var a) ? a : null);
        }

        private sealed class IssueHandler : INovelErrorHandler
        {
            public readonly List<NovelIssueInfo> Issues = new();
            public void OnScenarioFaulted(NovelErrorInfo error) { }
            public void OnRuntimeIssue(NovelIssueInfo issue) => Issues.Add(issue);
        }

        private sealed class NullView : INovelView
        {
            public UniTask ShowMessageAsync(NovelLine line, CancellationToken ct) => UniTask.CompletedTask;
            public UniTask<int> ShowChoicesAsync(IReadOnlyList<string> options, CancellationToken ct) => UniTask.FromResult(0);
            public void SetMessageWindowVisible(bool visible) { }
            public void ClearMessage() { }
        }

        private sealed class EmptyCatalog : ICharacterCatalog
        {
            public bool TryGet(string speakerId, out CharacterEntry entry) { entry = default; return false; }
            public IEnumerable<CharacterKeyInfo> EnumerateEntries() => Array.Empty<CharacterKeyInfo>();
        }

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

        [UnityTest]
        public IEnumerator cinematic語彙がアセットを引いて再生し_停止は導出し_未定義は通知する() => UniTask.ToCoroutine(async () =>
        {
            var runner = new RecordingRunner();
            var loader = new DictLoader();
            loader.Assets["vignette"] = Asset(Step(CinematicSequence.StepKind.Play, CinematicSequenceAsset.EffectKind.Vignette, custom: true));
            runner.Playing.Add(typeof(VignetteEffect));
            var progress = new NovelPlaybackProgress();
            var handler = new IssueHandler();
            var module = new CinematicCommandModule(runner, loader, progress, handler);

            using var scenario = new NovelScenarioRunner(
                new ScenarioSource(new ResourcesTextAssetLoader()), new Router(), new NullView(),
                new IdentityTextResolver(), new EmptyCatalog(),
                errorHandler: handler,
                preambleSources: new IPreambleSource[]
                {
                    new PreambleSource(new ResourcesTextAssetLoader()),
                    new PreambleSource(new ResourcesTextAssetLoader(), CinematicCommandModule.PreambleKey),
                },
                commandModules: new INovelCommandModule[] { module },
                progress: progress);

            LogAssert.Expect(LogType.Warning, new Regex("missing_effect"));
            var result = await scenario.PlayAsync("test_cinematic", CancellationToken.None);

            Assert.That(result, Is.EqualTo(NovelResult.Completed));
            Assert.That(runner.Runs.Count, Is.EqualTo(2), "Enter + 導出 Exit。未定義キーは再生しない");
            var exit = StepsOf(runner.Runs[1]).Single();
            Assert.That(exit.Kind, Is.EqualTo(CinematicSequence.StepKind.Stop));
            Assert.That(exit.Type, Is.EqualTo(typeof(VignetteEffect)));

            var issue = handler.Issues.Single(i => i.Kind == NovelIssueKind.EffectNotFound);
            Assert.That(issue.Subject, Is.EqualTo("missing_effect"));
            Assert.That(issue.ScenarioKey, Is.EqualTo("test_cinematic"), "progress 経由で再生中キーが添えられる");
        });

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
