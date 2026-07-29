#nullable enable
#if NOVEL_ADDRESSABLES
using System.Threading;
using NUnit.Framework;
using Novel.Addressables;

namespace Novel.Tests
{
    // 未登録キー→null の負系は Addressables ランタイム設定が必要で、設定なしプロジェクトでは
    // 非同期エラーログがテスト境界を越えて漏れ無関係なテストを汚染するため、ここでは扱わない
    public class AddressablesTextAssetLoaderTests
    {
        [Test]
        public void LoadBytesAsync_キャンセル済みトークンはロード開始前にOperationCanceledExceptionを投げる()
        {
            var loader = new AddressablesTextAssetLoader();
            using var cts = new CancellationTokenSource();
            cts.Cancel();
            Assert.Throws<System.OperationCanceledException>(() =>
                loader.LoadBytesAsync("any-key", ".mrb", cts.Token).GetAwaiter().GetResult());
        }

        [Test]
        public void LoadTextAsync_キャンセル済みトークンはロード開始前にOperationCanceledExceptionを投げる()
        {
            var loader = new AddressablesTextAssetLoader();
            using var cts = new CancellationTokenSource();
            cts.Cancel();
            Assert.Throws<System.OperationCanceledException>(() =>
                loader.LoadTextAsync("any-key", cts.Token).GetAwaiter().GetResult());
        }
    }
}
#endif
