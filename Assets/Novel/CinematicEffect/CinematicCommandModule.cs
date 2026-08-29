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
    /// <summary><c>cinematic</c> 語彙の実体。キーの解決は <see cref="CinematicSequenceResolver"/>、再生は Director。</summary>
    [Routes]
    public sealed partial class CinematicCommandModule : INovelCommandModule
    {
        /// <summary>cinematic / cinematic_stop 糖衣を定義する preamble の Resources キー。</summary>
        public const string PreambleKey = "Novel/CinematicPreamble";

        private readonly CinematicEffectDirector _director;
        private readonly CinematicSequenceResolver _resolver;
        private readonly NovelPlaybackProgress _progress;

        public CinematicCommandModule(CinematicEffectDirector director, CinematicSequenceResolver resolver, NovelPlaybackProgress progress)
        {
            _director = director;
            _resolver = resolver;
            _progress = progress;
        }

        public void RegisterVocabulary(MRubyState state) => state.AddCommand<CinematicCommand>("cinematic");
        public IDisposable MapHandlers(ICommandSubscribable router) => MapTo(router);

        public async UniTask On(CinematicCommand cmd, CancellationToken ct)
        {
            // 演出は瞬間表現なので早送り (セーブ復帰) では再現しない (world_effect と同じ扱い)
            if (_progress.IsFastForwarding) return;
            var sequence = await _resolver.ResolveAsync(cmd.Key, cmd.Stop, _director.IsPlaying, ct);
            if (sequence != null) await _director.RunAsync(sequence, ct);
        }
    }
}
