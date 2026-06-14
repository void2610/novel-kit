using NUnit.Framework;
using Novel.Runtime;

namespace Novel.Tests
{
    public sealed class BacklogTests
    {
        [Test]
        public void 追加した行を古い順に保持する()
        {
            var backlog = new RingBufferBacklog();
            backlog.Add("alice", "<color=#f88>一</color>");
            backlog.Add("", "二");

            Assert.AreEqual(2, backlog.Count);
            Assert.AreEqual("alice", backlog.Entries[0].Speaker);
            Assert.AreEqual("<color=#f88>一</color>", backlog.Entries[0].Text);   // rich のまま保持
            Assert.AreEqual("", backlog.Entries[1].Speaker);
        }

        [Test]
        public void 上限を超えたら最古から捨てる()
        {
            var backlog = new RingBufferBacklog(maxLines: 3);
            for (int i = 0; i < 5; i++) backlog.Add("s", i.ToString());

            Assert.AreEqual(3, backlog.Count);
            Assert.AreEqual("2", backlog.Entries[0].Text);   // 0,1 は退避
            Assert.AreEqual("4", backlog.Entries[2].Text);
        }

        [Test]
        public void Clearで全消去する()
        {
            var backlog = new RingBufferBacklog();
            backlog.Add("s", "a");
            backlog.Clear();
            Assert.AreEqual(0, backlog.Count);
        }
    }
}
