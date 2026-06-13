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
    // 「命令されたら 1 シナリオを再生する」プリミティブ。進行・章/分岐は game 所有（flow-boundary）
    public sealed class NovelScenarioRunner : INovelScenarioRunner
    {
        private readonly IScenarioSource _source;
        private readonly Router _router;             // container 所有・ハンドラは game の RegisterVitalRouter でマップ済み
        private readonly INovelErrorHandler? _errorHandler;
        private readonly MRubyState _state;          // 実行器私有（所有を 1 つに決め切る対象は Router のみ）

        public NovelScenarioRunner(IScenarioSource source, Router router, INovelErrorHandler? errorHandler = null)
        {
            _source = source;
            _router = router;
            _errorHandler = errorHandler;
            _state = MRubyState.Create();

            // Ruby cmd 名 → C# コマンド型。preamble.rb の糖衣が発行する名前と一致させる
            _state.DefineVitalRouter(config =>
            {
                config.AddCommand<SayCommand>("say");
                // TODO: choose/flag/portrait/bg/still/se/bgm/wait 等のリッチ統一語彙を追加（dsl-vocabulary）
            });

            // TODO: preamble.rb（say/narration/choose 等の糖衣）を起動時に一度だけ state へ評価する
        }

        public async UniTask<NovelResult> PlayAsync(string scenarioKey, CancellationToken ct)
        {
            try
            {
                var bytecode = await _source.LoadBytecodeAsync(scenarioKey, ct);
                if (bytecode == null || bytecode.Length == 0) return NovelResult.Completed;

                var irep = _state.ParseBytecode(bytecode);
                await _state.ExecuteAsync(_router, irep, ct);
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
    }
}
