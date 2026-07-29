#nullable enable
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using Novel.Runtime;
using Novel.View;

namespace Novel.Tests
{
    public class TextAssetSourceTests
    {
        private sealed class FakeTextAssetLoader : ITextAssetLoader
        {
            public readonly List<(string key, string suffix)> BytesRequests = new();
            public readonly List<string> TextRequests = new();
            public byte[]? BytesResult;
            public string? TextResult;

            public UniTask<byte[]?> LoadBytesAsync(string key, string subAssetSuffix, CancellationToken ct)
            {
                BytesRequests.Add((key, subAssetSuffix));
                return UniTask.FromResult(BytesResult);
            }

            public UniTask<string?> LoadTextAsync(string key, CancellationToken ct)
            {
                TextRequests.Add(key);
                return UniTask.FromResult(TextResult);
            }
        }

        [Test]
        public void ScenarioSource_Rootとキーとmrbサフィックスでローダーへ委譲する()
        {
            var loader = new FakeTextAssetLoader { BytesResult = new byte[] { 1, 2 } };
            var source = new ScenarioSource(loader, "Scenarios/");

            var result = source.LoadBytecodeAsync("BossDefeated", CancellationToken.None).GetAwaiter().GetResult();

            Assert.That(result, Is.EqualTo(new byte[] { 1, 2 }));
            Assert.That(loader.BytesRequests, Is.EqualTo(new[] { ("Scenarios/BossDefeated", ".mrb") }));
        }

        [Test]
        public void ScenarioSource_空キーはローダーを呼ばずnullを返す()
        {
            var loader = new FakeTextAssetLoader { BytesResult = new byte[] { 1 } };
            var source = new ScenarioSource(loader);

            var result = source.LoadBytecodeAsync("", CancellationToken.None).GetAwaiter().GetResult();

            Assert.That(result, Is.Null);
            Assert.That(loader.BytesRequests, Is.Empty);
        }

        [Test]
        public void PreambleSource_既定パスとmrbサフィックスでローダーへ委譲する()
        {
            var loader = new FakeTextAssetLoader { BytesResult = new byte[] { 3 } };
            var source = new PreambleSource(loader);

            var result = source.LoadPreambleAsync(CancellationToken.None).GetAwaiter().GetResult();

            Assert.That(result, Is.EqualTo(new byte[] { 3 }));
            Assert.That(loader.BytesRequests, Is.EqualTo(new[] { ("Novel/Preamble", ".mrb") }));
        }

        [Test]
        public void RubyDictionary_ローダーから定義を読み込んでルビを付与する()
        {
            var loader = new FakeTextAssetLoader { TextResult = "ruby '庭', 'にわ'" };
            var dict = new RubyDictionary();

            dict.LoadFromAsync(loader, "Novel/ruby", CancellationToken.None).GetAwaiter().GetResult();

            Assert.That(loader.TextRequests, Is.EqualTo(new[] { "Novel/ruby" }));
            Assert.That(dict.Entries, Has.Count.EqualTo(1));
            Assert.That(dict.ApplyTo("庭"), Does.Contain("にわ"));
        }

        [Test]
        public void ResourcesLoader_キャンセル済みトークンはcanceledなUniTaskを返す()
        {
            // Addressables 版と同じく「await で OperationCanceledException」に統一されている契約の固定
            var loader = new Novel.View.ResourcesTextAssetLoader();
            using var cts = new CancellationTokenSource();
            cts.Cancel();
            Assert.Throws<System.OperationCanceledException>(() =>
                loader.LoadBytesAsync("any", ".mrb", cts.Token).GetAwaiter().GetResult());
            Assert.Throws<System.OperationCanceledException>(() =>
                loader.LoadTextAsync("any", cts.Token).GetAwaiter().GetResult());
        }

        [Test]
        public void RubyDictionary_キーが見つからなければ空辞書のままになる()
        {
            var loader = new FakeTextAssetLoader { TextResult = null };
            var dict = new RubyDictionary();

            dict.LoadFromAsync(loader, "Novel/ruby", CancellationToken.None).GetAwaiter().GetResult();

            Assert.That(dict.Entries, Is.Empty);
            Assert.That(dict.ApplyTo("庭"), Is.EqualTo("庭"));
        }
    }
}
