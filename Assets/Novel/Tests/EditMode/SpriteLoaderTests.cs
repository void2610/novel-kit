#nullable enable
using System.Threading;
using NUnit.Framework;
using Novel.Assets;

namespace Novel.Tests
{
    public class SpriteLoaderTests
    {
        [Test]
        public void Resources_未登録キーはnullを返す()
        {
            var loader = new ResourcesSpriteLoader();
            var result = loader.LoadAsync("novel-kit-tests/unknown-sprite", CancellationToken.None)
                .GetAwaiter().GetResult();
            Assert.That(result, Is.Null);
        }

        [Test]
        public void Resources_空キーはロードを試みずnullを返す()
        {
            var loader = new ResourcesSpriteLoader("Novel/");
            var result = loader.LoadAsync("", CancellationToken.None).GetAwaiter().GetResult();
            Assert.That(result, Is.Null);
        }

        [Test]
        public void Resources_キャンセル済みトークンはOperationCanceledExceptionを投げる()
        {
            var loader = new ResourcesSpriteLoader();
            using var cts = new CancellationTokenSource();
            cts.Cancel();
            Assert.Throws<System.OperationCanceledException>(() =>
                loader.LoadAsync("any", cts.Token).GetAwaiter().GetResult());
        }

        [Test]
        public void Resources_ReleaseAllは例外を投げない()
        {
            var loader = new ResourcesSpriteLoader();
            Assert.DoesNotThrow(() => loader.ReleaseAll());
        }

#if NOVEL_ADDRESSABLES
        [Test]
        public void Addressables_キャンセル済みトークンはロード開始前にOperationCanceledExceptionを投げる()
        {
            var loader = new Novel.Addressables.AddressablesSpriteLoader();
            using var cts = new CancellationTokenSource();
            cts.Cancel();
            Assert.Throws<System.OperationCanceledException>(() =>
                loader.LoadAsync("any", cts.Token).GetAwaiter().GetResult());
        }

        [Test]
        public void Addressables_空キーはロードを試みずnullを返す()
        {
            var loader = new Novel.Addressables.AddressablesSpriteLoader();
            var result = loader.LoadAsync("", CancellationToken.None).GetAwaiter().GetResult();
            Assert.That(result, Is.Null);
        }

        [Test]
        public void Addressables_未ロード状態のReleaseAllは例外を投げない()
        {
            var loader = new Novel.Addressables.AddressablesSpriteLoader();
            Assert.DoesNotThrow(() => loader.ReleaseAll());
        }
#endif
    }
}
