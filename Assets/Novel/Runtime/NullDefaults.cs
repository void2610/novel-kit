#nullable enable
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;

namespace Novel.Runtime
{
    // 未配線時に注入できる no-op 既定（dsl-vocabulary: 未配線は握りつぶす）。DI で省略可能依存を埋める用途

    public sealed class NullPortraitView : IPortraitView
    {
        public UniTask SwitchLayoutAsync(PortraitLayout layout, CancellationToken ct) => UniTask.CompletedTask;
        public UniTask ShowAsync(int slotIndex, string character, string portraitKey, CancellationToken ct) => UniTask.CompletedTask;
        public UniTask HideAsync(int slotIndex, CancellationToken ct) => UniTask.CompletedTask;
    }

    public sealed class NullPortraitDirector : IPortraitDirector
    {
        public UniTask StageAsync(PortraitLayout layout, System.Collections.Generic.IReadOnlyList<string> cast, CancellationToken ct) => UniTask.CompletedTask;
        public UniTask StageAsync(PortraitLayout layout, System.Collections.Generic.IReadOnlyDictionary<string, int> cast, CancellationToken ct) => UniTask.CompletedTask;
        public UniTask ShowAsync(string character, string portraitKey, CancellationToken ct) => UniTask.CompletedTask;
        public UniTask ExitAsync(string character, CancellationToken ct) => UniTask.CompletedTask;
        public UniTask ClearStageAsync(CancellationToken ct) => UniTask.CompletedTask;
    }

    public sealed class NullBackgroundView : IBackgroundView
    {
        public UniTask ShowAsync(string backgroundKey, CancellationToken ct) => UniTask.CompletedTask;
        public UniTask ShowStillAsync(string stillKey, CancellationToken ct) => UniTask.CompletedTask;
    }

    public sealed class NullCenterImageView : ICenterImageView
    {
        public UniTask ShowAsync(string imageKey, CancellationToken ct) => UniTask.CompletedTask;
        public UniTask HideAsync(CancellationToken ct) => UniTask.CompletedTask;
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

    // 明示的に無音化したい game 向け（既定は View 層の Debug ログ実装。dsl-vocabulary の no-op とは別物）
    public sealed class NullErrorHandler : INovelErrorHandler
    {
        public void OnScenarioFaulted(NovelErrorInfo error) { }
    }
}
