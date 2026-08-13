#if NOVEL_LOCALIZATION
#nullable enable
using Novel.Localization;
using NUnit.Framework;

namespace Novel.Tests
{
    // MissingTextCollector は static (プロセス全体で共有) のため、各テストの前後で Clear して独立させる
    public sealed class MissingTextCollectorTests
    {
        [SetUp]
        public void SetUp() => MissingTextCollector.Clear();

        [TearDown]
        public void TearDown() => MissingTextCollector.Clear();

        [Test]
        public void Record_重複を除いて収集しSnapshotは順序安定()
        {
            MissingTextCollector.Record("b");
            MissingTextCollector.Record("a");
            MissingTextCollector.Record("b");

            Assert.That(MissingTextCollector.Snapshot(), Is.EqualTo(new[] { "a", "b" }));
        }

        [Test]
        public void Clear_で空になる()
        {
            MissingTextCollector.Record("x");
            MissingTextCollector.Clear();

            Assert.That(MissingTextCollector.Snapshot(), Is.Empty);
        }

        [Test]
        public void Record_はSessionState区切り文字を除去して保存する()
        {
            // U+001F は SessionState 退避の区切り文字。含まれたまま保存すると復元時に偽エントリへ分裂する
            MissingTextCollector.Record("こん\u001fにちは");

            Assert.That(MissingTextCollector.Snapshot(), Is.EqualTo(new[] { "こんにちは" }));
        }
    }
}
#endif
