using System.Collections.Generic;
using NUnit.Framework;
using Novel.Runtime;

namespace Novel.Tests
{
    public sealed class NovelSaveSerializerTests
    {
        private static NovelStateSnapshot Snapshot(Dictionary<string, int> values, params string[] read)
            => new(values, read);

        [Test]
        public void 値と既読を往復できる()
        {
            var snap = Snapshot(
                new Dictionary<string, int> { { "coins", 30 }, { "met_taylor", 1 } },
                "a1b2c3d4e5f60718", "0000000000000001");

            var json = NovelSaveSerializer.Serialize(snap);
            Assert.IsTrue(NovelSaveSerializer.TryDeserialize(json, out var back));

            Assert.AreEqual(2, back.Values.Count);
            Assert.AreEqual(30, back.Values["coins"]);
            Assert.AreEqual(1, back.Values["met_taylor"]);
            CollectionAssert.AreEquivalent(new[] { "a1b2c3d4e5f60718", "0000000000000001" }, back.ReadTextIds);
        }

        [Test]
        public void 出力は決定的でキーが序数ソートされる()
        {
            var a = Snapshot(new Dictionary<string, int> { { "b", 2 }, { "a", 1 } }, "y", "x");
            var b = Snapshot(new Dictionary<string, int> { { "a", 1 }, { "b", 2 } }, "x", "y");

            // 挿入順が違っても同一 JSON(diff/テスト安定)
            Assert.AreEqual(NovelSaveSerializer.Serialize(a), NovelSaveSerializer.Serialize(b));
            Assert.AreEqual(
                "{\"version\":1,\"values\":{\"a\":1,\"b\":2},\"read\":[\"x\",\"y\"]}",
                NovelSaveSerializer.Serialize(a));
        }

        [Test]
        public void 空スナップショットを往復できる()
        {
            var json = NovelSaveSerializer.Serialize(NovelSaveSerializer.Empty);
            Assert.AreEqual("{\"version\":1,\"values\":{},\"read\":[]}", json);
            Assert.IsTrue(NovelSaveSerializer.TryDeserialize(json, out var back));
            Assert.AreEqual(0, back.Values.Count);
            Assert.AreEqual(0, back.ReadTextIds.Count);
        }

        [Test]
        public void 特殊文字とマイナス値をエスケープして往復する()
        {
            var snap = Snapshot(
                new Dictionary<string, int> { { "quote\"and\\slash", -5 }, { "new\nline", 0 } },
                "tab\tid", "日本語");

            var json = NovelSaveSerializer.Serialize(snap);
            Assert.IsTrue(NovelSaveSerializer.TryDeserialize(json, out var back));

            Assert.AreEqual(-5, back.Values["quote\"and\\slash"]);
            Assert.AreEqual(0, back.Values["new\nline"]);
            CollectionAssert.AreEquivalent(new[] { "tab\tid", "日本語" }, back.ReadTextIds);
        }

        [Test]
        public void 破損jsonはfalseと空を返す()
        {
            Assert.IsFalse(NovelSaveSerializer.TryDeserialize("{ not json", out var back));
            Assert.AreEqual(0, back.Values.Count);
            Assert.AreEqual(0, back.ReadTextIds.Count);
        }

        [Test]
        public void nullや空文字はfalseと空を返す()
        {
            Assert.IsFalse(NovelSaveSerializer.TryDeserialize(null, out _));
            Assert.IsFalse(NovelSaveSerializer.TryDeserialize("", out _));
            Assert.IsFalse(NovelSaveSerializer.TryDeserialize("   ", out _));
        }

        [Test]
        public void フィールド欠落や未知フィールドを許容する()
        {
            // values/read が無くても空として復元、未知フィールドは無視
            Assert.IsTrue(NovelSaveSerializer.TryDeserialize(
                "{\"version\":1,\"extra\":{\"x\":1},\"values\":{\"a\":7}}", out var back));
            Assert.AreEqual(7, back.Values["a"]);
            Assert.AreEqual(0, back.ReadTextIds.Count);
        }

        [Test]
        public void ルートが非オブジェクトなら厳密読み取りは例外()
        {
            Assert.Throws<NovelSaveFormatException>(() => NovelSaveSerializer.Deserialize("[1,2,3]"));
        }
    }
}
