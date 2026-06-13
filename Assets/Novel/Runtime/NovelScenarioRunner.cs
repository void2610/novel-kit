#nullable enable
using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using MRubyCS;
using Novel.Commands;
using VitalRouter;
using VitalRouter.MRuby;

namespace Novel.Runtime
{
    // 「命令されたら 1 シナリオを再生する」プリミティブ。進行・章/分岐は game 所有（flow-boundary）。
    // FIXME(後で直す): router-ownership ADR は handler を game の RegisterVitalRouter でマップする想定だが、
    // handler が runner 私有の MRubyStateStore（共有テーブル背後）に依存するため、ここでは runner が
    // handler を構築し注入された Router へ MapTo している。IStateStore の所有権含め後で再整理する。
    public sealed class NovelScenarioRunner : INovelScenarioRunner, IDisposable
    {
        private readonly IScenarioSource _source;
        private readonly Router _router;
        private readonly ISaveStore? _saveStore;
        private readonly INovelErrorHandler? _errorHandler;
        private readonly IPreambleSource? _preambleSource;
        private readonly MRubyState _state;
        private readonly MRubyStateStore _store;
        private readonly IDisposable _subscription;
        private bool _preambleLoaded;
        private bool _disposed;

        public NovelScenarioRunner(IScenarioSource source, Router router,
            INovelView view, ITextResolver text, ICharacterCatalog catalog,
            IPortraitView? portrait = null, IBackgroundView? background = null, IAudioChannel? audio = null,
            IWorldEffectSink? worldEffectSink = null,
            ISaveStore? saveStore = null, INovelErrorHandler? errorHandler = null,
            IPreambleSource? preambleSource = null)
        {
            _source = source;
            _router = router;
            _saveStore = saveStore;
            _errorHandler = errorHandler;
            _preambleSource = preambleSource;
            _state = MRubyState.Create();
            _store = new MRubyStateStore(_state.GetSharedVariables());

            _state.DefineVitalRouter(config =>
            {
                config.AddCommand<SayCommand>("say");
                config.AddCommand<ChooseCommand>("choose");
                config.AddCommand<FlagCommand>("flag");
                config.AddCommand<PortraitCommand>("portrait");
                config.AddCommand<BackgroundCommand>("bg");
                config.AddCommand<StillCommand>("still");
                config.AddCommand<SeCommand>("se");
                config.AddCommand<BgmCommand>("bgm");
                config.AddCommand<WaitCommand>("wait");
            });

            var handler = new NovelCommandHandler(view, _store, text, catalog, portrait, background, audio, worldEffectSink);
            _subscription = handler.MapTo(_router);
        }

        public async UniTask<NovelResult> PlayAsync(string scenarioKey, CancellationToken ct)
        {
            try
            {
                await EnsurePreambleLoadedAsync(ct);

                if (_saveStore != null)
                {
                    var snapshot = await _saveStore.LoadAsync(ct);
                    _store.Restore(snapshot);
                }

                var bytecode = await _source.LoadBytecodeAsync(scenarioKey, ct);
                if (bytecode == null || bytecode.Length == 0) return NovelResult.Completed;

                var irep = _state.ParseBytecode(bytecode);
                await _state.ExecuteAsync(_router, irep, ct);

                // セーブ境界は PlayAsync の狭間（save-snapshot）
                if (_saveStore != null) await _saveStore.SaveAsync(_store.Capture(), ct);

                return NovelResult.Completed;
            }
            catch (OperationCanceledException)
            {
                return NovelResult.Cancelled;
            }
            catch (Exception ex)
            {
                // backtrace を surface しつつフェイルセーフで Faulted を返す（error-handling）
                _errorHandler?.OnScenarioFaulted(scenarioKey, ex);
                return NovelResult.Faulted;
            }
        }

        // 糖衣ヘルパ（say/choose 等）を定義する preamble を起動時に一度だけ state へ評価する
        private async UniTask EnsurePreambleLoadedAsync(CancellationToken ct)
        {
            if (_preambleLoaded || _preambleSource == null) return;
            _preambleLoaded = true;
            var bytecode = await _preambleSource.LoadPreambleAsync(ct);
            if (bytecode != null && bytecode.Length > 0) _state.LoadBytecode(bytecode);
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            _subscription.Dispose();
        }
    }
}
