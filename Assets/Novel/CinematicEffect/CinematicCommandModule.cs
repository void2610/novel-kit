#nullable enable
using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using Novel.Runtime;
using VitalRouter;
using Void2610.CinematicEffect;

namespace Novel.Cinematic
{
    /// <summary>
    /// <c>cinematic</c> 語彙の実体。キー → アセット (<c>&lt;key&gt;</c> / <c>&lt;key&gt;_exit</c>) を引いて Director で再生する。
    /// 止め方を Enter から推測して代行することはしない (演出の中身と同じくプロジェクトが決める)。
    /// </summary>
    [Routes]
    public sealed partial class CinematicCommandModule : INovelCommandModule, IDisposable
    {
        public const string ExitSuffix = "_exit";

        /// <summary>cinematic / cinematic_stop 糖衣を定義する preamble の Resources キー。</summary>
        public const string PreambleKey = "Novel/CinematicPreamble";

        private readonly CinematicEffectDirector _director;
        private readonly ICinematicSequenceLoader _loader;
        private readonly NovelPlaybackProgress _progress;
        private readonly INovelErrorHandler? _errorHandler;

        // 早送り中に通過した cinematic の結果。復帰地点でまだ続いている演出だけを再現する
        private readonly CinematicHeldState _heldDuringFastForward = new();
        private CancellationTokenRegistration _fastForwardCancelRegistration;

        public CinematicCommandModule(CinematicEffectDirector director, ICinematicSequenceLoader loader,
            NovelPlaybackProgress progress, INovelErrorHandler? errorHandler = null)
        {
            _director = director;
            _loader = loader;
            _progress = progress;
            _errorHandler = errorHandler;
            _progress.FastForwardReached += RestoreHeldEffects;
        }

        public void Dispose() => _progress.FastForwardReached -= RestoreHeldEffects;

        private void RestoreHeldEffects()
        {
            if (!_heldDuringFastForward.IsEmpty) _director.RunAsync(_heldDuringFastForward.BuildPlaySequence()).Forget();
            _heldDuringFastForward.Clear();
        }

        public void RegisterVocabulary(INovelVocabulary vocabulary) => vocabulary.Add<CinematicCommand>("cinematic");
        public IDisposable MapHandlers(ICommandSubscribable router) => MapTo(router);

        public async UniTask On(CinematicCommand cmd, CancellationToken ct)
        {
            if (string.IsNullOrEmpty(cmd.Key)) return;

            var assetKey = cmd.Stop ? cmd.Key + ExitSuffix : cmd.Key;
            var asset = await _loader.LoadAsync(assetKey, ct);
            if (asset == null)
            {
                NovelDiagnostics.EffectNotFound(_errorHandler, _progress.ScenarioKey, assetKey,
                    $"Resources/{ResourcesCinematicSequenceLoader.Root}{assetKey}.asset を置いてください (使えるキーは Novel > Project Reference で確認できます)。");
                return;
            }
            if (_progress.IsFastForwarding)
            {
                // 途中経過は見せず結果だけ覚える。早送りが打ち切られたら次回へ持ち越さない
                _heldDuringFastForward.Apply(asset.Build());
                _fastForwardCancelRegistration.Dispose();
                _fastForwardCancelRegistration = ct.Register(_heldDuringFastForward.Clear);
                return;
            }
            await _director.RunAsync(asset.Build(), ct);
        }
    }
}
