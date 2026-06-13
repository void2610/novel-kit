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

            public UniTask ShowMessageAsync(NovelLine line, CancellationToken ct)
            {
                Lines.Add(line);
                return UniTask.CompletedTask;
            }

            public UniTask<int> ShowChoicesAsync(IReadOnlyList<string> options, CancellationToken ct)
                => UniTask.FromResult(0);
        }

        private sealed class EmptyCatalog : ICharacterCatalog
        {
            public bool TryGet(string speakerId, out CharacterEntry entry)
            {
                entry = default;
                return false;
            }
        }

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
    }
}
