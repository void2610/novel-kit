#nullable enable
using System.Threading;
using Cysharp.Threading.Tasks;
using Novel.Runtime;
using UnityEngine;

namespace Novel.View
{
    // dev ビルド（Editor / Development Build）で「コマンドは来たが対応 View が未供給」を一度だけ警告する no-op 既定。
    // dsl-vocabulary の no-op（未配線でも .rb は常に動く）を保ちつつ、無言ドロップによる「なぜ出ないのか分からない」
    // デバッグ地獄を避ける（埋め込み用途での配線忘れに気づける）。本番ビルドでは黙る。
    public sealed class WarningPortraitView : IPortraitView
    {
        private bool _warned;
        public UniTask SwitchLayoutAsync(PortraitLayout layout, CancellationToken ct) { Warn(); return UniTask.CompletedTask; }
        public UniTask ShowAsync(int slotIndex, string character, string portraitKey, CancellationToken ct) { Warn(); return UniTask.CompletedTask; }
        public UniTask HideAsync(int slotIndex, CancellationToken ct) { Warn(); return UniTask.CompletedTask; }
        private void Warn() => FacetWarning.Once(ref _warned, "portrait/stage", "IPortraitView");
    }

    public sealed class WarningBackgroundView : IBackgroundView
    {
        private bool _warned;
        public UniTask ShowAsync(string backgroundKey, CancellationToken ct) { Warn(); return UniTask.CompletedTask; }
        public UniTask ShowStillAsync(string stillKey, CancellationToken ct) { Warn(); return UniTask.CompletedTask; }
        private void Warn() => FacetWarning.Once(ref _warned, "bg/still", "IBackgroundView");
    }

    public sealed class WarningAudioChannel : IAudioChannel
    {
        private bool _warned;
        public UniTask PlaySeAsync(string seKey, CancellationToken ct) { Warn(); return UniTask.CompletedTask; }
        public void PlayBgm(string bgmKey) => Warn();
        public void StopBgm() => Warn();
        private void Warn() => FacetWarning.Once(ref _warned, "se/bgm", "IAudioChannel");
    }

    internal static class FacetWarning
    {
        public static void Once(ref bool warned, string command, string facet)
        {
            if (warned || !Debug.isDebugBuild) return;
            warned = true;
            Debug.LogWarning($"[Novel] {command} コマンドを受けましたが {facet} が未供給のため無視しました（no-op）。" +
                             $"演出を出すには game が {facet} を登録してください。");
        }
    }
}
