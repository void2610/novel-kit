#nullable enable
using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;

namespace Novel.Runtime
{
    // 未配線時に注入できる no-op 既定（dsl-vocabulary: 未配線は握りつぶす）。DI で省略可能依存を埋める用途

    public sealed class NullPortraitView : IPortraitView
    {
        public UniTask ShowAsync(string portraitKey, CancellationToken ct) => UniTask.CompletedTask;
        public UniTask HideAsync(CancellationToken ct) => UniTask.CompletedTask;
    }

    public sealed class NullBackgroundView : IBackgroundView
    {
        public UniTask ShowAsync(string backgroundKey, CancellationToken ct) => UniTask.CompletedTask;
        public UniTask ShowStillAsync(string stillKey, CancellationToken ct) => UniTask.CompletedTask;
    }

    public sealed class NullAudioChannel : IAudioChannel
    {
        public UniTask PlaySeAsync(string seKey, CancellationToken ct) => UniTask.CompletedTask;
        public void PlayBgm(string bgmKey) { }
        public void StopBgm() { }
    }

    public sealed class NullWorldEffectSink : IWorldEffectSink
    {
        public UniTask DispatchAsync(IWorldEffect effect, CancellationToken ct) => UniTask.CompletedTask;
    }

    public sealed class NullSaveStore : ISaveStore
    {
        public UniTask SaveAsync(NovelStateSnapshot snapshot, CancellationToken ct) => UniTask.CompletedTask;

        public UniTask<NovelStateSnapshot> LoadAsync(CancellationToken ct)
            => UniTask.FromResult(new NovelStateSnapshot(new Dictionary<string, int>(), Array.Empty<string>()));
    }

    public sealed class NullErrorHandler : INovelErrorHandler
    {
        public void OnScenarioFaulted(string scenarioKey, Exception exception) { }
    }
}
