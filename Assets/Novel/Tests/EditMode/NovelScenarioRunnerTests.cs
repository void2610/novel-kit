using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Cysharp.Threading.Tasks;
using MRubyCS;
using MRubyCS.Serializer;
using Novel.Runtime;
using Novel.View;
using NUnit.Framework;
using UnityEngine.TestTools;
using VitalRouter;
using VitalRouter.MRuby;

namespace Novel.Tests
{
    // 独自コマンドの語彙束縛 + ハンドラ写像を 1 クラスに束ねる拡張口の検証用コマンド/モジュール
    [MRubyObject]
    public readonly partial record struct CustomEchoCommand : ICommand
    {
        public string Text { get; init; }
    }

    [Routes]
    public sealed partial class CustomEchoModule : INovelCommandModule
    {
        public readonly List<string> Received = new();
        public void RegisterVocabulary(MRubyState state) => state.AddCommand<CustomEchoCommand>("custom_echo");
        public IDisposable MapHandlers(ICommandSubscribable router) => MapTo(router);
        public void On(CustomEchoCommand cmd) => Received.Add(cmd.Text);
    }

    public sealed class NovelScenarioRunnerTests
    {
        private sealed class FakeView : INovelView
        {
            public readonly List<NovelLine> Lines = new();
            public int ChoiceResult;

            public UniTask ShowMessageAsync(NovelLine line, CancellationToken ct)
            {
                Lines.Add(line);
                return UniTask.CompletedTask;
            }

            public UniTask<int> ShowChoicesAsync(IReadOnlyList<string> options, CancellationToken ct)
                => UniTask.FromResult(ChoiceResult);
        }

        // ShowMessageAsync を gate が解放されるまでブロックする View（再入テスト用）
        private sealed class GatedView : INovelView
        {
            private readonly UniTaskCompletionSource _gate;
            public GatedView(UniTaskCompletionSource gate) => _gate = gate;

            public UniTask ShowMessageAsync(NovelLine line, CancellationToken ct) => _gate.Task;
            public UniTask<int> ShowChoicesAsync(IReadOnlyList<string> options, CancellationToken ct) => UniTask.FromResult(0);
        }

        private sealed class EmptyCatalog : ICharacterCatalog
        {
            public bool TryGet(string speakerId, out CharacterEntry entry)
            {
                entry = default;
                return false;
            }
        }

        // インメモリのセーブストア（境界保存→復元の往復検証用）
        private sealed class MemorySaveStore : ISaveStore
        {
            private NovelStateSnapshot _saved = new(new Dictionary<string, int>(), Array.Empty<string>());
            public NovelStateSnapshot Saved => _saved;

            public UniTask SaveAsync(NovelStateSnapshot snapshot, CancellationToken ct)
            {
                _saved = snapshot;
                return UniTask.CompletedTask;
            }

            public UniTask<NovelStateSnapshot> LoadAsync(CancellationToken ct) => UniTask.FromResult(_saved);
        }

        private sealed class FakeErrorHandler : INovelErrorHandler
        {
            public bool Called;
            public string? Key;

            public void OnScenarioFaulted(NovelErrorInfo error)
            {
                Called = true;
                Key = error.ScenarioKey;
            }
        }

        private static NovelScenarioRunner NewRunner(INovelView view, ISaveStore? saveStore = null)
            => new(new ResourcesScenarioSource(), new Router(), view,
                new IdentityTextResolver(), new EmptyCatalog(),
                saveStore: saveStore, preambleSources: new IPreambleSource[] { new ResourcesPreambleSource() });

        [UnityTest]
        public IEnumerator シナリオを実行し_say_が_View_へ順に届く() => UniTask.ToCoroutine(async () =>
        {
            var view = new FakeView();
            var runner = new NovelScenarioRunner(
                new ResourcesScenarioSource(),
                new Router(),
                view,
                new IdentityTextResolver(),
                new EmptyCatalog(),
                preambleSources: new IPreambleSource[] { new ResourcesPreambleSource() });

            var result = await runner.PlayAsync("test_hello", CancellationToken.None);

            Assert.AreEqual(NovelResult.Completed, result);
            Assert.AreEqual(2, view.Lines.Count);
            Assert.AreEqual("こんにちは", view.Lines[0].Text);
            Assert.AreEqual("alice", view.Lines[0].DisplayName);   // カタログ未登録 → id をそのまま表示名
            Assert.IsNull(view.Lines[1].DisplayName);              // narration はナレーション
        });

        // choose の結果が共有テーブル経由で Ruby の state[:key] に読み戻り分岐が成立することを検証
        [UnityTest]
        public IEnumerator choose_の選択が_Ruby_側の分岐へ反映される() => UniTask.ToCoroutine(async () =>
        {
            var view = new FakeView { ChoiceResult = 1 };   // B を選択
            var runner = new NovelScenarioRunner(
                new ResourcesScenarioSource(),
                new Router(),
                view,
                new IdentityTextResolver(),
                new EmptyCatalog(),
                preambleSources: new IPreambleSource[] { new ResourcesPreambleSource() });

            var result = await runner.PlayAsync("test_choose", CancellationToken.None);

            Assert.AreEqual(NovelResult.Completed, result);
            Assert.AreEqual(1, view.Lines.Count);
            Assert.AreEqual("Bを選んだ", view.Lines[0].Text);
        });

        // flag 設定 → セーブ境界で保存 → 別 runner で復元 → Ruby の state[:key] が読めることを検証
        [UnityTest]
        public IEnumerator flag_がセーブ境界で保存され復元後に_Ruby_から読める() => UniTask.ToCoroutine(async () =>
        {
            var save = new MemorySaveStore();

            var setResult = await NewRunner(new FakeView(), save).PlayAsync("test_flag_set", CancellationToken.None);
            Assert.AreEqual(NovelResult.Completed, setResult);
            Assert.AreEqual(5, save.Saved.Values["score"]);   // 境界で保存された

            var view = new FakeView();
            var readResult = await NewRunner(view, save).PlayAsync("test_flag_read", CancellationToken.None);
            Assert.AreEqual(NovelResult.Completed, readResult);
            Assert.AreEqual("5", view.Lines[0].Text);          // 復元後に Ruby が読み戻せた
        });

        // __ 始まりの choose 自動キーはセーブ境界のスナップショットから除外し、明示キーと flag は永続することを固定（回帰防止）
        [UnityTest]
        public IEnumerator choose自動キーはセーブ除外され明示キーとflagは永続する() => UniTask.ToCoroutine(async () =>
        {
            var save = new MemorySaveStore();

            var result = await NewRunner(new FakeView { ChoiceResult = 1 }, save)
                .PlayAsync("test_choose_keys", CancellationToken.None);

            Assert.AreEqual(NovelResult.Completed, result);
            var keys = save.Saved.Values.Keys;
            Assert.IsTrue(keys.Contains("picked"));   // 明示キーは永続
            Assert.IsTrue(keys.Contains("kept"));     // flag は永続
            Assert.IsFalse(keys.Any(k => k.StartsWith("__", StringComparison.Ordinal)));   // 自動採番は除外
        });

        // 再生中（前の PlayAsync 完了前）の再入は InvalidOperationException で弾くことを検証（単一 MRubyState 共有）
        [UnityTest]
        public IEnumerator 再生中の再入PlayAsyncは例外を投げる() => UniTask.ToCoroutine(async () =>
        {
            var gate = new UniTaskCompletionSource();
            var runner = NewRunner(new GatedView(gate));
            var first = runner.PlayAsync("test_hello", CancellationToken.None);   // 最初の say で gate 待ちに入る

            var threw = false;
            try { await runner.PlayAsync("test_hello", CancellationToken.None); }
            catch (InvalidOperationException) { threw = true; }
            Assert.IsTrue(threw);   // 再入は弾かれる

            gate.TrySetResult();     // 解放して 1 本目を完了させる
            Assert.AreEqual(NovelResult.Completed, await first);
        });

        // MRuby 実行時例外で Faulted を返し INovelErrorHandler へ委譲することを検証
        [UnityTest]
        public IEnumerator MRuby例外で_Faulted_を返し_ErrorHandler_へ委譲する() => UniTask.ToCoroutine(async () =>
        {
            var handler = new FakeErrorHandler();
            var runner = new NovelScenarioRunner(
                new ResourcesScenarioSource(),
                new Router(),
                new FakeView(),
                new IdentityTextResolver(),
                new EmptyCatalog(),
                errorHandler: handler,
                preambleSources: new IPreambleSource[] { new ResourcesPreambleSource() });

            var result = await runner.PlayAsync("test_error", CancellationToken.None);

            Assert.AreEqual(NovelResult.Faulted, result);
            Assert.IsTrue(handler.Called);
            Assert.AreEqual("test_error", handler.Key);
        });

        // INovelCommandModule が独自コマンドの語彙束縛とハンドラ写像を差し込めることを検証（拡張口）
        [UnityTest]
        public IEnumerator 独自コマンドモジュールが語彙とハンドラを差し込める() => UniTask.ToCoroutine(async () =>
        {
            var module = new CustomEchoModule();
            var runner = new NovelScenarioRunner(
                new ResourcesScenarioSource(),
                new Router(),
                new FakeView(),
                new IdentityTextResolver(),
                new EmptyCatalog(),
                preambleSources: new IPreambleSource[] { new ResourcesPreambleSource() },
                commandModules: new INovelCommandModule[] { module });

            var result = await runner.PlayAsync("test_custom_command", CancellationToken.None);

            Assert.AreEqual(NovelResult.Completed, result);
            CollectionAssert.AreEqual(new[] { "echoed" }, module.Received);   // 独自 cmd がハンドラへ届いた
        });
    }
}

namespace System.Runtime.CompilerServices
{
    // record struct の init アクセサ用ポリフィル（テストアセンブリは Novel.Commands の内包版を共有しないため）
    internal static class IsExternalInit { }
}
