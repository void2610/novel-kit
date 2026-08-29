#nullable enable
using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using MRubyCS;
using Novel.Runtime;
using VitalRouter;
using VitalRouter.MRuby;
using Void2610.CinematicEffect;

namespace Novel.Cinematic
{
    /// <summary>
    /// <c>cinematic</c> 語彙の実体。キー → アセット (Enter / Exit) を引いて Director で再生する。
    /// Exit は <c>&lt;key&gt;_exit</c> があればそれ、無ければ Enter から導出する (<see cref="CinematicExitDeriver"/>)。
    /// </summary>
    [Routes]
    public sealed partial class CinematicCommandModule : INovelCommandModule
    {
        public const string ExitSuffix = "_exit";

        /// <summary>cinematic / cinematic_stop 糖衣を定義する preamble の Resources キー。</summary>
        public const string PreambleKey = "Novel/CinematicPreamble";

        private readonly CinematicEffectDirector _director;
        private readonly ICinematicSequenceLoader _loader;
        private readonly NovelPlaybackProgress _progress;
        private readonly INovelErrorHandler? _errorHandler;

        public CinematicCommandModule(CinematicEffectDirector director, ICinematicSequenceLoader loader,
            NovelPlaybackProgress progress, INovelErrorHandler? errorHandler = null)
        {
            _director = director;
            _loader = loader;
            _progress = progress;
            _errorHandler = errorHandler;
        }

        public void RegisterVocabulary(MRubyState state) => state.AddCommand<CinematicCommand>("cinematic");
        public IDisposable MapHandlers(ICommandSubscribable router) => MapTo(router);

        public async UniTask On(CinematicCommand cmd, CancellationToken ct)
        {
            // 演出は瞬間表現なので早送り (セーブ復帰) では再現しない (world_effect と同じ扱い)
            if (_progress.IsFastForwarding) return;
            if (string.IsNullOrEmpty(cmd.Key)) return;

            var sequence = cmd.Stop ? await ResolveExitAsync(cmd.Key, ct) : await ResolveEnterAsync(cmd.Key, ct);
            if (sequence != null) await _director.RunAsync(sequence, ct);
        }

        private async UniTask<CinematicSequence?> ResolveEnterAsync(string key, CancellationToken ct)
        {
            var asset = await _loader.LoadAsync(key, ct);
            if (asset != null) return asset.Build();
            NotFound(key, $"Resources/{ResourcesCinematicSequenceLoader.Root}{key}.asset を置いてください (使えるキーは Novel > Project Reference で確認できます)。");
            return null;
        }

        private async UniTask<CinematicSequence?> ResolveExitAsync(string key, CancellationToken ct)
        {
            var exit = await _loader.LoadAsync(key + ExitSuffix, ct);
            if (exit != null) return exit.Build();

            var enter = await _loader.LoadAsync(key, ct);
            if (enter == null)
            {
                NotFound(key, $"停止対象の {key} も {key}{ExitSuffix} も Resources/{ResourcesCinematicSequenceLoader.Root} にありません。");
                return null;
            }
            // 導出結果が空 (一発物・既に停止済み) なら何もしないのが正しいので警告しない
            return CinematicExitDeriver.Derive(enter, _director.IsPlaying);
        }

        private void NotFound(string key, string hint) =>
            NovelDiagnostics.EffectNotFound(_errorHandler, _progress.ScenarioKey, key, hint);
    }
}
