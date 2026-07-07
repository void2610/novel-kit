using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using Novel.Integration;
using Novel.Runtime;
using NUnit.Framework;
using VContainer;

namespace Novel.Tests
{
    public sealed class NovelContainerExtensionsTests
    {
        private sealed class StubView : INovelView
        {
            public UniTask ShowMessageAsync(NovelLine line, CancellationToken ct) => UniTask.CompletedTask;
            public UniTask<int> ShowChoicesAsync(IReadOnlyList<string> options, CancellationToken ct) => UniTask.FromResult(0);
            public void SetMessageWindowVisible(bool visible) { }
        }

        private sealed class StubCatalog : ICharacterCatalog
        {
            public bool TryGet(string speakerId, out CharacterEntry entry)
            {
                entry = default;
                return false;
            }
        }

        private sealed class StubSource : IScenarioSource
        {
            public UniTask<byte[]?> LoadBytecodeAsync(string scenarioKey, CancellationToken ct)
                => UniTask.FromResult<byte[]?>(null);
        }

        // RegisterNovelKitCore で実際にコンテナを Build し、依存グラフ検証を通って runner/backlog を解決できることを固定。
        // RingBufferBacklog(int maxLines=200) を型登録すると int 未解決で Build が落ちる回帰を防ぐ。
        [Test]
        public void RegisterNovelKitCore_でBuildしrunnerとbacklogを解決できる()
        {
            var builder = new ContainerBuilder();
            builder.RegisterNovelKitCore();
            builder.RegisterInstance<INovelView>(new StubView());
            builder.RegisterInstance<ICharacterCatalog>(new StubCatalog());
            builder.RegisterInstance<IScenarioSource>(new StubSource());

            using var container = builder.Build();   // ここで依存グラフ検証が走る

            Assert.IsInstanceOf<NovelScenarioRunner>(container.Resolve<INovelScenarioRunner>());
            Assert.IsInstanceOf<RingBufferBacklog>(container.Resolve<IBacklog>());
        }
    }
}
