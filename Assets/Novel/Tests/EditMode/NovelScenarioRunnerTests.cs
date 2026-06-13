using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using Novel.Runtime;
using Novel.View;
using NUnit.Framework;
using UnityEngine.TestTools;
using VitalRouter;

namespace Novel.Tests
{
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

        private static NovelScenarioRunner NewRunner(INovelView view, ISaveStore? saveStore = null)
            => new(new ResourcesScenarioSource(), new Router(), view,
                new IdentityTextResolver(), new EmptyCatalog(),
                saveStore: saveStore, preambleSource: new ResourcesPreambleSource());

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
                preambleSource: new ResourcesPreambleSource());

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
                preambleSource: new ResourcesPreambleSource());

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
    }
}
