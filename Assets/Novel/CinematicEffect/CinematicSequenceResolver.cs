#nullable enable
using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using Novel.Runtime;
using Void2610.CinematicEffect;

namespace Novel.Cinematic
{
    /// <summary>
    /// 演出キーから再生すべき <see cref="CinematicSequence"/> を決める (Director を触らない純粋ロジック)。
    /// Enter は <c>&lt;key&gt;</c>、Exit は <c>&lt;key&gt;_exit</c> があればそれ、無ければ Enter から導出する
    /// (<see cref="CinematicExitDeriver"/>)。見つからなければ診断を出して null。
    /// </summary>
    public sealed class CinematicSequenceResolver
    {
        public const string ExitSuffix = "_exit";

        private readonly ICinematicSequenceLoader _loader;
        private readonly NovelPlaybackProgress _progress;
        private readonly INovelErrorHandler? _errorHandler;

        public CinematicSequenceResolver(ICinematicSequenceLoader loader, NovelPlaybackProgress progress,
            INovelErrorHandler? errorHandler = null)
        {
            _loader = loader;
            _progress = progress;
            _errorHandler = errorHandler;
        }

        /// <param name="isPlaying">導出 Exit で「動いているものだけ止める」ための判定 (Director.IsPlaying)。null なら全部止める</param>
        public async UniTask<CinematicSequence?> ResolveAsync(string key, bool stop, Func<Type, bool>? isPlaying, CancellationToken ct)
        {
            if (string.IsNullOrEmpty(key)) return null;
            if (!stop)
            {
                var enter = await _loader.LoadAsync(key, ct);
                if (enter != null) return enter.Build();
                NotFound(key, $"Resources/{ResourcesCinematicSequenceLoader.Root}{key}.asset を置いてください (使えるキーは Novel > Project Reference で確認できます)。");
                return null;
            }

            var exit = await _loader.LoadAsync(key + ExitSuffix, ct);
            if (exit != null) return exit.Build();

            var source = await _loader.LoadAsync(key, ct);
            if (source == null)
            {
                NotFound(key, $"停止対象の {key} も {key}{ExitSuffix} も Resources/{ResourcesCinematicSequenceLoader.Root} にありません。");
                return null;
            }
            // 導出結果が空 (一発物・既に停止済み) なら何もしないのが正しいので警告しない
            return CinematicExitDeriver.Derive(source, isPlaying);
        }

        private void NotFound(string key, string hint) =>
            NovelDiagnostics.EffectNotFound(_errorHandler, _progress.ScenarioKey, key, hint);
    }
}
