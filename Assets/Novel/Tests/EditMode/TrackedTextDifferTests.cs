#nullable enable
using System.Linq;
using Novel.Editor.Localization;
using NUnit.Framework;

namespace Novel.Tests
{
    public sealed class TrackedTextDifferTests
    {
        [Test]
        public void 誤字修正は高類似のFuzzyCarryとして対になる()
        {
            var diff = TrackedTextDiffer.Diff(
                new[] { "本気で言っているの？", "そうだよ。" },
                new[] { "本気で言っているの…？", "そうだよ。" });

            Assert.AreEqual(1, diff.Unchanged.Count);
            Assert.AreEqual(1, diff.Changes.Count);
            Assert.AreEqual(TextChangeKind.FuzzyCarry, diff.Changes[0].Kind);
            CollectionAssert.IsEmpty(diff.AddedCurrent);
            CollectionAssert.IsEmpty(diff.RemovedPrevious);
        }

        [Test]
        public void タグだけの変更はTagOnlyとして訳保持対象になる()
        {
            var diff = TrackedTextDiffer.Diff(
                new[] { "驚いたな、本当に来たのか" },
                new[] { "驚いたな、<w=0.4>本当に来たのか" });

            Assert.AreEqual(TextChangeKind.TagOnly, diff.Changes.Single().Kind);
        }

        [Test]
        public void 中程度の書き直しはRewrittenに分類される()
        {
            var diff = TrackedTextDiffer.Diff(
                new[] { "明日の朝、駅前で待ち合わせしよう" },
                new[] { "明日の朝は駅の改札に集合ね" });

            var change = diff.Changes.Single();
            Assert.AreEqual(TextChangeKind.Rewritten, change.Kind);
            Assert.Less(change.Similarity, TrackedTextDiffer.FuzzyThreshold);
        }

        [Test]
        public void 全く別のテキストは対にならず追加と削除になる()
        {
            var diff = TrackedTextDiffer.Diff(
                new[] { "こんにちは、いい天気だね" },
                new[] { "0123456789" });

            CollectionAssert.IsEmpty(diff.Changes);
            Assert.AreEqual(1, diff.AddedCurrent.Count);
            Assert.AreEqual(1, diff.RemovedPrevious.Count);
        }

        [Test]
        public void 行の挿入で位置がずれても既存行は無影響でアンカーされる()
        {
            var diff = TrackedTextDiffer.Diff(
                new[] { "一行目です", "二行目です", "三行目です" },
                new[] { "一行目です", "挿入された新しい行", "二行目です", "三行目です" });

            Assert.AreEqual(3, diff.Unchanged.Count);
            CollectionAssert.IsEmpty(diff.Changes);
            CollectionAssert.AreEqual(new[] { 1 }, diff.AddedCurrent);
        }

        [Test]
        public void 行分割は重なりの大きい側にcarryされ他方は新規になる()
        {
            var diff = TrackedTextDiffer.Diff(
                new[] { "前の行だよ", "今日は朝から雨が降っていて、外に出る気になれなかった", "次の行だよ" },
                new[] { "前の行だよ", "今日は朝から雨が降っていた", "外に出る気になれなかったんだ", "次の行だよ" });

            Assert.AreEqual(2, diff.Unchanged.Count);
            Assert.AreEqual(1, diff.Changes.Count);          // 類似の高い片方だけが対になる
            Assert.AreEqual(1, diff.AddedCurrent.Count);     // もう片方は新規
            CollectionAssert.IsEmpty(diff.RemovedPrevious);
        }

        [Test]
        public void 連続する複数行の同時変更も位置ギャップ内で対応付けられる()
        {
            var diff = TrackedTextDiffer.Diff(
                new[] { "変わらない行", "海はどこまでも青かった", "風が少し冷たかった", "変わらない行その2" },
                new[] { "変わらない行", "海はどこまでも青かったんだ", "風がずいぶん冷たかった", "変わらない行その2" });

            Assert.AreEqual(2, diff.Unchanged.Count);
            Assert.AreEqual(2, diff.Changes.Count);
            // 順序が入れ替わらず、それぞれ自分の書き直しと対になっている
            var byPrev = diff.Changes.OrderBy(c => c.PreviousIndex).ToArray();
            Assert.AreEqual(byPrev[0].PreviousIndex + 0, 1);
            Assert.AreEqual(byPrev[0].CurrentIndex, 1);
            Assert.AreEqual(byPrev[1].PreviousIndex, 2);
            Assert.AreEqual(byPrev[1].CurrentIndex, 2);
        }
    }
}
