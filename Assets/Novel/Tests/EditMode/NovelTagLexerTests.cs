using System.Collections.Generic;
using NUnit.Framework;
using Novel.Runtime;

namespace Novel.Tests
{
    public sealed class NovelTagLexerTests
    {
        [Test]
        public void ToPlainText_全タグを除去する()
        {
            var plain = NovelTagLexer.ToPlainText("a<color=#fff>b</color><w=1>c");
            Assert.AreEqual("abc", plain);
        }

        [Test]
        public void Parse_制御タグを制御トークンへ抽出する()
        {
            var tokens = new List<NovelToken>(NovelTagLexer.Parse("<w=0.5>x<p>"));
            Assert.AreEqual(NovelTokenKind.Wait, tokens[0].Kind);
            Assert.AreEqual(0.5f, tokens[0].Value, 0.0001f);
            Assert.AreEqual(NovelTokenKind.Text, tokens[1].Kind);
            Assert.AreEqual("x", tokens[1].Payload);
            Assert.AreEqual(NovelTokenKind.ClickWait, tokens[2].Kind);
        }

        [Test]
        public void Parse_TMPスタイルは素通しする()
        {
            var tokens = new List<NovelToken>(NovelTagLexer.Parse("<color=#f88>赤</color>"));
            Assert.AreEqual(NovelTokenKind.TmpTag, tokens[0].Kind);
            Assert.AreEqual("<color=#f88>", tokens[0].Payload);
            Assert.AreEqual(NovelTokenKind.TmpTag, tokens[2].Kind);
            Assert.AreEqual("</color>", tokens[2].Payload);
        }

        [Test]
        public void Parse_shake開閉とspeed倍率を解釈する()
        {
            var tokens = new List<NovelToken>(NovelTagLexer.Parse("<speed=2x>速<shake>揺</shake></speed>"));
            Assert.AreEqual(NovelTokenKind.SpeedPush, tokens[0].Kind);
            Assert.AreEqual(2f, tokens[0].Value, 0.0001f);
            Assert.AreEqual(NovelTokenKind.ShakePush, tokens[2].Kind);
            Assert.AreEqual(NovelTokenKind.ShakePop, tokens[4].Kind);
            Assert.AreEqual(NovelTokenKind.SpeedPop, tokens[5].Kind);
        }

        [Test]
        public void Parse_未知タグはリテラル文字として扱う()
        {
            var plain = NovelTagLexer.ToPlainText("a<unknown>b");
            Assert.AreEqual("a<unknown>b", plain);
        }

        [Test]
        public void Parse_noparse区間は内部の制御タグもリテラル化する()
        {
            // 内部の <w=1> は Wait トークンにならずリテラル文字として残る
            var plain = NovelTagLexer.ToPlainText("前<noparse>foo<w=1></noparse>後");
            Assert.AreEqual("前foo<w=1>後", plain);

            foreach (var t in NovelTagLexer.Parse("前<noparse>foo<w=1></noparse>後"))
                Assert.AreNotEqual(NovelTokenKind.Wait, t.Kind);
        }

        [Test]
        public void Parse_閉じないnoparseは残り全部をリテラル化する()
        {
            var plain = NovelTagLexer.ToPlainText("a<noparse>b<color=#fff>c");
            Assert.AreEqual("ab<color=#fff>c", plain);
        }
    }
}
