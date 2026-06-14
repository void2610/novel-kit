#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Cysharp.Threading.Tasks;
using MRubyCS;
using Novel.Commands;
using VitalRouter;
using VitalRouter.MRuby;

namespace Novel.Runtime
{
    // 「命令されたら 1 シナリオを再生する」プリミティブ。進行・章/分岐は game 所有（flow-boundary）。
    // handler は runner 私有の MRubyStateStore（MRuby 共有テーブル背後）に結合するため runner が構築し
    // 注入された Router へ MapTo する。Router は container 登録・IStateStore は runtime 内部（永続は ISaveStore）。
    // → router-ownership / state-model ADR で確定。
    public sealed class NovelScenarioRunner : INovelScenarioRunner, IDisposable
    {
        private readonly IScenarioSource _source;
        private readonly Router _router;
        private readonly ISaveStore? _saveStore;
        private readonly INovelErrorHandler? _errorHandler;
        private readonly IReadOnlyList<IPreambleSource> _preambleSources;
        private readonly MRubyState _state;
        private readonly MRubyStateStore _store;
        private readonly List<IDisposable> _subscriptions;
        private bool _preambleLoaded;
        private bool _playing;
        private bool _disposed;

        public NovelScenarioRunner(IScenarioSource source, Router router,
            INovelView view, ITextResolver text, ICharacterCatalog catalog,
            IPortraitView? portrait = null, IBackgroundView? background = null, IAudioChannel? audio = null,
            IWorldEffectSink? worldEffectSink = null,
            ISaveStore? saveStore = null, INovelErrorHandler? errorHandler = null,
            IEnumerable<IPreambleSource>? preambleSources = null,
            IEnumerable<INovelCommandModule>? commandModules = null)
        {
            _source = source;
            _router = router;
            _saveStore = saveStore;
            _errorHandler = errorHandler;
            _preambleSources = preambleSources?.ToArray() ?? Array.Empty<IPreambleSource>();
            var modules = commandModules?.ToArray() ?? Array.Empty<INovelCommandModule>();
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
                config.AddCommand<WorldEffectCommand>("world_effect");
                // game 独自コマンドの語彙束縛（組込語彙の後・名前衝突は game 責任）
                foreach (var module in modules) module.RegisterVocabulary(config);
            });

            var handler = new NovelCommandHandler(view, _store, text, catalog, portrait, background, audio, worldEffectSink);
            _subscriptions = new List<IDisposable> { handler.MapTo(_router) };
            // 独自コマンドハンドラを同じノベル専用 Router へ写像（購読は Dispose でまとめて解除）
            foreach (var module in modules) _subscriptions.Add(module.MapHandlers(_router));
        }

        public async UniTask<NovelResult> PlayAsync(string scenarioKey, CancellationToken ct)
        {
            // 単一 MRubyState を共有するため再入/同時再生はできない。完了前の再呼び出しは fail-fast する
            if (_playing)
                throw new InvalidOperationException(
                    "NovelScenarioRunner は前の PlayAsync 完了前に再生できません（単一 MRubyState 共有のため）。");
            _playing = true;
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
                // Ruby backtrace を含めて surface しつつフェイルセーフで Faulted を返す（error-handling）
                _errorHandler?.OnScenarioFaulted(NovelErrorReport.Describe(scenarioKey, ex));
                return NovelResult.Faulted;
            }
            finally
            {
                _playing = false;
            }
        }

        // 糖衣ヘルパ（say/choose 等）を定義する preamble を起動時に一度だけ state へ評価する。
        // 登録順に評価するため、組込糖衣が先・game 追加糖衣が後になる（getting-started の登録順契約）。
        private async UniTask EnsurePreambleLoadedAsync(CancellationToken ct)
        {
            if (_preambleLoaded) return;
            // 例外/キャンセル時はフラグを立てず次回再試行する（途中失敗で糖衣未定義のまま恒久 Faulted になるのを防ぐ）
            foreach (var preambleSource in _preambleSources)
            {
                var bytecode = await preambleSource.LoadPreambleAsync(ct);
                if (bytecode != null && bytecode.Length > 0) _state.LoadBytecode(bytecode);
            }
            _preambleLoaded = true;   // 全 preamble ロード成功（または不在の確定結果）後にのみ確定
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            foreach (var subscription in _subscriptions) subscription.Dispose();
        }
    }
}
