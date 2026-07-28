#nullable enable
using NUnit.Framework;
using Novel.Runtime;
using Novel.View;

namespace Novel.Tests
{
    public class NovelDisplayTextTests
    {
        [Test]
        public void Build_PlainText_WrapsInNoparse()
        {
            var tokens = NovelTagLexer.Parse("こんにちは");
            Assert.That(NovelDisplayText.Build(tokens), Is.EqualTo("<noparse>こんにちは</noparse>"));
        }

        [Test]
        public void Build_ControlTags_AreStripped()
        {
            var tokens = NovelTagLexer.Parse("待つ<w=1.5>よ<p>ね");
            Assert.That(NovelDisplayText.Build(tokens),
                Is.EqualTo("<noparse>待つ</noparse><noparse>よ</noparse><noparse>ね</noparse>"));
        }

        [Test]
        public void Build_TmpTags_ArePassedThrough()
        {
            var tokens = NovelTagLexer.Parse("<color=red>赤</color>い");
            Assert.That(NovelDisplayText.Build(tokens),
                Is.EqualTo("<color=red><noparse>赤</noparse></color><noparse>い</noparse>"));
        }

        [Test]
        public void Build_Ruby_ExpandsToOverlay()
        {
            var tokens = NovelTagLexer.Parse("<ruby=にわ>庭</ruby>師");
            var result = NovelDisplayText.Build(tokens);
            Assert.That(result, Is.EqualTo(RubyMarkup.BuildOverlay("庭", "にわ") + "<noparse>師</noparse>"));
        }

        [Test]
        public void Build_LiteralAngleBracket_IsNoparsed()
        {
            var tokens = NovelTagLexer.Parse("1 < 2");
            Assert.That(NovelDisplayText.Build(tokens), Is.EqualTo("<noparse>1 < 2</noparse>"));
        }
    }
}
