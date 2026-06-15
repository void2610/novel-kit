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
        private bool _disposed;

        // switch-latest 直列化用: 進行中の再生の「完了(teardown)通知」と、その中断用トークン源。
        // 単一 MRubyState を共有するため、再入時は前再生を cancel し完了通知を待ってから差し替える。
        // 戻り値タスクではなく専用の通知ソースで待ち合わせ、同一 UniTask の二重 await を避ける。
        private UniTaskCompletionSource? _previousDone;
        private CancellationTokenSource? _inFlightCts;

        public NovelScenarioRunner(IScenarioSource source, Router router,
            INovelView view, ITextResolver text, ICharacterCatalog catalog,
            IPortraitView? portrait = null, IBackgroundView? background = null, IAudioChannel? audio = null,
            IWorldEffectSink? worldEffectSink = null,
            ISaveStore? saveStore = null, INovelErrorHandler? errorHandler = null,
            IEnumerable<IPreambleSource>? preambleSources = null,
            IEnumerable<INovelCommandModule>? commandModules = null,
            IBacklog? backlog = null)
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

            var handler = new NovelCommandHandler(view, _store, text, catalog, portrait, background, audio, worldEffectSink, backlog);
            _subscriptions = new List<IDisposable> { handler.MapTo(_router) };
            // 独自コマンドハンドラを同じノベル専用 Router へ写像（購読は Dispose でまとめて解除）
            foreach (var module in modules) _subscriptions.Add(module.MapHandlers(_router));
        }

        // 命令されたら 1 シナリオを再生する。再生中に再度呼ぶと switch-latest:
        // 進行中の再生を cancel し、その後始末（単一 MRubyState の巻き戻し）完了を待ってから新シナリオへ差し替える。
        // 差し替えられた前呼び出しは NovelResult.Cancelled を受け取る。呼び出し側は直列化を意識しなくてよい。
        public UniTask<NovelResult> PlayAsync(string scenarioKey, CancellationToken ct)
        {
            if (_disposed) throw new ObjectDisposedException(nameof(NovelScenarioRunner));

            // 同期的に「自分の完了通知」と中断トークンを差し込んでから返す
            // (Unity 単一スレッドのため、次の呼び出しは必ず更新後の値を見て自分を直列に繋げられる)
            var previousDone = _previousDone;
            var previousCts = _inFlightCts;
            var myDone = new UniTaskCompletionSource();
            var myCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            _previousDone = myDone;
            _inFlightCts = myCts;
            return RunChainedAsync(previousDone, previousCts, scenarioKey, myCts, myDone);
        }

        // 直前の再生を中断し、その完了通知を待ってから自分を直列に実行する。
        // 戻り値タスクではなく専用の完了通知(UTCS)で待ち合わせるため、同一 UniTask を二重 await しない
        // (UniTask の単一 await 制約を回避する)。前 cts は中断 → 待機後にここで破棄する
        // (後継が前任を破棄する規約: 破棄後 Cancel / 二重破棄を避ける)。
        private async UniTask<NovelResult> RunChainedAsync(
            UniTaskCompletionSource? previousDone, CancellationTokenSource? previousCts,
            string scenarioKey, CancellationTokenSource myCts, UniTaskCompletionSource myDone)
        {
            previousCts?.Cancel();
            if (previousDone != null)
            {
                try { await previousDone.Task; }
                catch { /* 前再生の結果/例外は前呼び出しが受け取る。ここは teardown 完了の同期だけが目的 */ }
            }
            previousCts?.Dispose();

            try
            {
                return await RunOnceAsync(scenarioKey, myCts.Token);
            }
            finally
            {
                myDone.TrySetResult();   // 自分の完了(teardown 完走)を次の再生へ通知する
            }
        }

        // 1 シナリオの実体。例外は握って NovelResult.Faulted/Cancelled に畳む（フェイルセーフ）。
        private async UniTask<NovelResult> RunOnceAsync(string scenarioKey, CancellationToken ct)
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
                // Ruby backtrace を含めて surface しつつフェイルセーフで Faulted を返す（error-handling）
                _errorHandler?.OnScenarioFaulted(NovelErrorReport.Describe(scenarioKey, ex));
                return NovelResult.Faulted;
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
            // 進行中の再生を中断し、最後の cts（後継がいないため未破棄のまま残る）を破棄する
            if (_inFlightCts != null)
            {
                _inFlightCts.Cancel();
                _inFlightCts.Dispose();
                _inFlightCts = null;
            }
            foreach (var subscription in _subscriptions) subscription.Dispose();
        }
    }
}
