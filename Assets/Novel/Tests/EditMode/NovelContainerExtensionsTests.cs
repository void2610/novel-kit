#nullable enable
using System;
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
            public void ClearMessage() { }
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

        // 親スコープに Scoped で一度登録し、シーン相当の子スコープごとに独立したインスタンスを得る契約を固定する。
        // runner は解決したスコープが生成するため、インスタンスが分かれることは各 runner が
        // その子スコープ側の INovelView / ファセットに束縛されることと同義。
        [Test]
        public void Scopedなら子スコープごとに別のrunnerとbacklogを得る()
        {
            var builder = new ContainerBuilder();
            builder.RegisterNovelKitCore(Lifetime.Scoped);
            builder.RegisterInstance<ICharacterCatalog>(new StubCatalog());
            builder.RegisterInstance<IScenarioSource>(new StubSource());

            using var root = builder.Build();
            var viewA = new StubView();
            var viewB = new StubView();
            using var scopeA = root.CreateScope(b => b.RegisterInstance<INovelView>(viewA));
            using var scopeB = root.CreateScope(b => b.RegisterInstance<INovelView>(viewB));

            Assert.That(scopeA.Resolve<INovelView>(), Is.SameAs(viewA));
            Assert.That(scopeB.Resolve<INovelView>(), Is.SameAs(viewB));
            Assert.That(scopeA.Resolve<INovelScenarioRunner>(),
                Is.Not.SameAs(scopeB.Resolve<INovelScenarioRunner>()));
            Assert.That(scopeA.Resolve<IBacklog>(), Is.Not.SameAs(scopeB.Resolve<IBacklog>()));
        }

        [Test]
        public void Scopedでも同じスコープ内では同じインスタンスを返す()
        {
            var builder = new ContainerBuilder();
            builder.RegisterNovelKitCore(Lifetime.Scoped);
            builder.RegisterInstance<ICharacterCatalog>(new StubCatalog());
            builder.RegisterInstance<IScenarioSource>(new StubSource());

            using var root = builder.Build();
            using var scope = root.CreateScope(b => b.RegisterInstance<INovelView>(new StubView()));

            Assert.That(scope.Resolve<INovelScenarioRunner>(),
                Is.SameAs(scope.Resolve<INovelScenarioRunner>()));
        }

        [Test]
        public void Transientは受け付けない()
        {
            var builder = new ContainerBuilder();

            Assert.Throws<ArgumentOutOfRangeException>(() => builder.RegisterNovelKitCore(Lifetime.Transient));
        }
    }
}
