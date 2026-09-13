#nullable enable
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Novel.Runtime;

namespace Novel.Tests
{
    // ルビ定義 (.rb) の解析と本文への付与ロジックそのものを固定する。
    // handler 経由の配線 (表示テキストにだけ付く / 平文・既読 ID・バックログに混入しない) は RubyApplicationTests、
    // インラインタグ <ruby=よみ>親</ruby> の経路は NovelTagLexerTests / NovelDisplayTextTests が持つ。
    public sealed class RubyMarkupTests
    {
        [Test]
        public void Parse_コメント行を無視しシングルとダブル両方のクォートを読む()
        {
            var rb = "# ルビ定義\nruby '工房', 'こうぼう'\n# ruby 'これはコメント', 'むし'\nruby \"祖父\", \"そふ\"\n";
            var entries = RubyMarkup.Parse(rb).ToList();

            Assert.That(entries, Has.Count.EqualTo(2), "# で始まる行は定義として読まない");
            Assert.That(entries[0].Base, Is.EqualTo("工房"));
            Assert.That(entries[0].Reading, Is.EqualTo("こうぼう"));
            Assert.That(entries[1].Base, Is.EqualTo("祖父"));
            Assert.That(entries[1].Reading, Is.EqualTo("そふ"));
        }

        [Test]
        public void Parse_表示モードを読む()
        {
            var rb = "ruby '工房', 'こうぼう'\n"
                + "ruby '摩天楼', 'まてんろう', :first\n"
                + "ruby '書庫', 'しょこ', :once\n"
                + "ruby '記憶', 'きおく', :always\n";
            var entries = RubyMarkup.Parse(rb).ToList();

            Assert.That(entries[0].FirstOnly, Is.False, "省略時は常に表示");
            Assert.That(entries[1].FirstOnly, Is.True, ":first は初出のみ");
            Assert.That(entries[2].FirstOnly, Is.True, ":once は :first の同義語");
            Assert.That(entries[3].FirstOnly, Is.False, ":always は常に表示");
        }

        [Test]
        public void ToRichText_よみを縮小サイズで重ねつつ親文字も残す()
        {
            var entries = new List<RubyEntry> { new RubyEntry("工房", "こうぼう") };

            var result = RubyMarkup.ToRichText("古い工房に篭る", entries);

            Assert.That(result, Does.Contain("<size=50%>"), "よみは親文字に対する縮小サイズで描く");
            Assert.That(result, Does.Contain("こうぼう"));
            Assert.That(result, Does.Contain("工房"), "よみで親文字を置き換えてはいけない");
            Assert.That(result, Does.Contain("古い"));
            Assert.That(result, Does.Contain("篭る"));
        }

        [Test]
        public void ToRichText_リッチテキストタグを壊さない()
        {
            // タグで囲まれた親文字にはルビが付くが、タグ自体は素通しし、タグの中身は親文字として扱わない
            var entries = new List<RubyEntry> { new RubyEntry("記憶", "きおく"), new RubyEntry("memory", "メモリ") };

            var result = RubyMarkup.ToRichText("<link=\"kw:memory\"><color=#38E0FF>記憶</color></link>", entries);

            Assert.That(result, Does.Contain("<link=\"kw:memory\">"), "タグは 1 文字も書き換えない");
            Assert.That(result, Does.Contain("</link>"));
            Assert.That(result, Does.Contain("<color=#38E0FF>"));
            Assert.That(result, Does.Contain("きおく"), "タグに囲まれた親文字にはルビが付く");
            Assert.That(result, Does.Not.Contain("メモリ"), "タグ内部の文字列は親文字として拾わない");
        }

        [Test]
        public void ToRichText_親文字は最長一致を優先する()
        {
            // ToRichText は各位置で渡された順に試すため、親文字長の降順で渡すのは呼び出し側の責務。
            // 自動 は 自動人形 の先頭に重なるので、順序を守らないと短い方が先取りしてしまう。
            var entries = new List<RubyEntry> { new RubyEntry("自動人形", "オートマタ"), new RubyEntry("自動", "じどう") };

            var result = RubyMarkup.ToRichText("彼女は自動人形だ", entries);

            Assert.That(result, Does.Contain("オートマタ"));
            Assert.That(result, Does.Not.Contain("じどう"), "短い 自動 が 自動人形 を先取りしない");
        }

        [Test]
        public void RubyDictionary_定義順によらず長い親文字を優先する()
        {
            // Load が親文字長の降順へ整列するので、先頭が重なる短い語を先に定義しても長い語が勝つ
            var dictionary = new RubyDictionary();
            dictionary.Load("ruby '自動', 'じどう'\nruby '自動人形', 'オートマタ'\n");

            var result = dictionary.ApplyTo("彼女は自動人形だ");

            Assert.That(result, Does.Contain("オートマタ"));
            Assert.That(result, Does.Not.Contain("じどう"), "定義順のままだと 自動 が先取りしてしまう");
        }

        [Test]
        public void ToRichText_付与しない語は親文字だけを出す()
        {
            var entries = new List<RubyEntry> { new RubyEntry("工房", "こうぼう", true) };

            var result = RubyMarkup.ToRichText("古い工房に篭る", entries, _ => false);

            Assert.That(result, Does.Contain("工房"), "親文字は残る");
            Assert.That(result, Does.Not.Contain("こうぼう"), "よみ (ルビ) は付かない");
        }

        [Test]
        public void RubyDictionary_初出のみの語は一度だけ付きResetShownで復帰する()
        {
            var dictionary = new RubyDictionary();
            dictionary.Load("ruby '工房', 'こうぼう', :first\nruby '記憶', 'きおく'\n");

            var first = dictionary.ApplyTo("工房と記憶");
            Assert.That(first, Does.Contain("こうぼう"));
            Assert.That(first, Does.Contain("きおく"));

            var second = dictionary.ApplyTo("工房と記憶");
            Assert.That(second, Does.Not.Contain("こうぼう"), "初出のみの語は 2 回目以降付かない");
            Assert.That(second, Does.Contain("きおく"), "常時表示の語は毎回付く");

            dictionary.ResetShown();
            Assert.That(dictionary.ApplyTo("工房"), Does.Contain("こうぼう"), "周回時は初出から再開する");
        }
    }
}
