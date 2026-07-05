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
                "{\"version\":1,\"values\":[{\"key\":\"a\",\"value\":1},{\"key\":\"b\",\"value\":2}],\"read\":[\"x\",\"y\"]}",
                NovelSaveSerializer.Serialize(a));
        }

        [Test]
        public void 空スナップショットを往復できる()
        {
            var json = NovelSaveSerializer.Serialize(NovelSaveSerializer.Empty);
            Assert.AreEqual("{\"version\":1,\"values\":[],\"read\":[]}", json);
            Assert.IsTrue(NovelSaveSerializer.TryDeserialize(json, out var back));
            Assert.AreEqual(0, back.Values.Count);
            Assert.AreEqual(0, back.ReadTextIds.Count);
        }

        [Test]
        public void default_snapshotはNRE無しで空として直列化できる()
        {
            // default(NovelStateSnapshot) は Values/ReadTextIds が null（未代入フィールド等）。
            // Serialize が NRE を投げず Empty 相当を出すこと。
            var json = NovelSaveSerializer.Serialize(default);
            Assert.AreEqual("{\"version\":1,\"values\":[],\"read\":[]}", json);
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
            // read 欠落は空として復元、未知フィールド(extra)は無視
            Assert.IsTrue(NovelSaveSerializer.TryDeserialize(
                "{\"version\":1,\"extra\":123,\"values\":[{\"key\":\"a\",\"value\":7}]}", out var back));
            Assert.AreEqual(7, back.Values["a"]);
            Assert.AreEqual(0, back.ReadTextIds.Count);
        }

        [Test]
        public void ルートが非オブジェクトなら厳密読み取りは例外()
        {
            Assert.Throws<NovelSaveFormatException>(() => NovelSaveSerializer.Deserialize("[1,2,3]"));
        }

        // read 配列の null/空要素はフィルタされる(破損/手編集セーブで不正 id が既読集合に混ざらない)
        [Test]
        public void readのnullや空要素はフィルタされる()
        {
            Assert.IsTrue(NovelSaveSerializer.TryDeserialize(
                "{\"version\":1,\"values\":[],\"read\":[\"\",\"x\",\"\"]}", out var back));
            CollectionAssert.AreEquivalent(new[] { "x" }, back.ReadTextIds);
        }

        // 自前 serde 用の公開クラス(NovelSaveData)経由の往復。ゲームは From/ToSnapshot で
        // snapshot ⇔ クラスを変換し、直列化は自分の serde に任せる。
        [Test]
        public void NovelSaveDataでsnapshotを往復できる()
        {
            var snap = Snapshot(
                new Dictionary<string, int> { { "b", 2 }, { "a", 1 } }, "y", "x");

            var data = NovelSaveData.From(snap);
            // 決定的: values/read が序数ソートされている
            Assert.AreEqual(new[] { "a", "b" }, new[] { data.values[0].key, data.values[1].key });
            Assert.AreEqual(new[] { "x", "y" }, data.read.ToArray());

            var back = data.ToSnapshot();
            Assert.AreEqual(1, back.Values["a"]);
            Assert.AreEqual(2, back.Values["b"]);
            CollectionAssert.AreEquivalent(new[] { "x", "y" }, back.ReadTextIds);
        }

        // NovelSaveData は JsonUtility 互換(プレーンフィールド)なので、ゲームの serde で往復できることを確認。
        [Test]
        public void NovelSaveDataはjson互換で往復できる()
        {
            var snap = Snapshot(new Dictionary<string, int> { { "coins", 30 } }, "id1");
            var json = UnityEngine.JsonUtility.ToJson(NovelSaveData.From(snap));
            var back = UnityEngine.JsonUtility.FromJson<NovelSaveData>(json).ToSnapshot();
            Assert.AreEqual(30, back.Values["coins"]);
            CollectionAssert.AreEquivalent(new[] { "id1" }, back.ReadTextIds);
        }
    }
}
