#nullable enable
using System.Linq;
using Novel.Editor;
using NUnit.Framework;

namespace Novel.Tests
{
    // project-reference ADR: キャラタブが「このキャラの立ち絵」をどう推定し、どう短縮表示するかを固定する
    public sealed class PortraitKeyGroupingTests
    {
        private static readonly string[] Keys =
        {
            "Characters/aria/default",
            "Characters/aria/smile",
            "Characters/noise/default",
            "Portraits/mia_angry",
            "Portraits/mia_smile",
            "bg/room",
        };

        [Test]
        public void 既定立ち絵と同じフォルダを優先してキャラの立ち絵を集める()
        {
            var rows = PortraitKeyGrouping.Collect("aria", "Characters/aria/default", Keys);

            Assert.That(rows.Select(r => r.Key), Is.EqualTo(new[] { "Characters/aria/default", "Characters/aria/smile" }));
            Assert.That(rows.Select(r => r.ShortName), Is.EqualTo(new[] { "default", "smile" }));
            Assert.That(rows.Single(r => r.IsDefault).Key, Is.EqualTo("Characters/aria/default"));
        }

        [Test]
        public void 既定立ち絵が無くてもパスセグメントのキャラidで集めて短縮する()
        {
            var rows = PortraitKeyGrouping.Collect("noise", null, Keys);

            Assert.That(rows.Select(r => r.Key), Is.EqualTo(new[] { "Characters/noise/default" }));
            Assert.That(rows.Single().ShortName, Is.EqualTo("default"));
            Assert.That(rows.Single().IsDefault, Is.False);
        }

        [Test]
        public void フォルダ分けが無い構成はファイル名の接頭辞で集めて短縮する()
        {
            var rows = PortraitKeyGrouping.Collect("mia", null, Keys);

            Assert.That(rows.Select(r => r.Key), Is.EqualTo(new[] { "Portraits/mia_angry", "Portraits/mia_smile" }));
            Assert.That(rows.Select(r => r.ShortName), Is.EqualTo(new[] { "angry", "smile" }));
        }

        [Test]
        public void 実体の無い既定立ち絵も宣言として先頭に載せる()
        {
            var rows = PortraitKeyGrouping.Collect("ghost", "Characters/ghost/default", Keys);

            Assert.That(rows.Single().Key, Is.EqualTo("Characters/ghost/default"));
            Assert.That(rows.Single().IsDefault, Is.True);
        }

        [Test]
        public void 手掛かりが無いキャラには立ち絵を割り当てない()
        {
            Assert.That(PortraitKeyGrouping.Collect("unknown", null, Keys), Is.Empty);
        }
    }
}
