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
    //
    // overlay の実体 (退避量・持ち上げ量・縮小率) を literal で固定するのは
    // ToRichText_よみを縮小サイズで重ねつつ親文字も残す の 1 件だけで、
    // 他は BuildOverlay を oracle にして「どこに何個 overlay が出るか」だけを見る (責務の分離)。
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

            Assert.That(entries, Has.Count.EqualTo(4), "モード付きの行も 1 件として読む");
            Assert.That(entries[0].FirstOnly, Is.False, "省略時は常に表示");
            Assert.That(entries[1].FirstOnly, Is.True, ":first は初出のみ");
            Assert.That(entries[2].FirstOnly, Is.True, ":once は :first の同義語");
            Assert.That(entries[3].FirstOnly, Is.False, ":always は常に表示");
        }

        [Test]
        public void ToRichText_よみを縮小サイズで重ねつつ親文字も残す()
        {
            // overlay の幾何を本体で唯一固定するテスト。
            // NovelDisplayTextTests.Build_Ruby_ExpandsToOverlay は BuildOverlay 自身を期待値に使うため、
            // 退避量 (<space=-Nem>) や持ち上げ量 (<voffset>) を変えても落ちない。ここだけが literal で pin する。
            var entries = new List<RubyEntry> { new RubyEntry("工房", "こうぼう") };

            // 親 2 字 / よみ 4 字 × 0.5 = 2em なので offset=0・back=0+2=2
            Assert.That(RubyMarkup.ToRichText("古い工房に篭る", entries), Is.EqualTo(
                "古い"
                + "<space=0em><voffset=0.9em><size=50%><noparse>こうぼう</noparse></size></voffset>"
                + "<space=-2em><noparse>工房</noparse>"
                + "に篭る"));

            // よみ幅が親幅を超える場合は offset を 0 へ切り上げる (親 1 字 / よみ 3 字 × 0.5 = 1.5em → back=1.5)
            var wide = new List<RubyEntry> { new RubyEntry("扉", "とびら") };
            Assert.That(RubyMarkup.ToRichText("扉", wide), Is.EqualTo(
                "<space=0em><voffset=0.9em><size=50%><noparse>とびら</noparse></size></voffset>"
                + "<space=-1.5em><noparse>扉</noparse>"));
        }

        [Test]
        public void ToRichText_リッチテキストタグを壊さない()
        {
            // タグで囲まれた親文字にはルビが付くが、タグ自体は 1 文字も書き換えず素通しし、
            // タグ名・属性値はどちらも親文字として拾わない (entries は親文字長の降順)。
            var entries = new List<RubyEntry> { new RubyEntry("link", "リンク"), new RubyEntry("記憶", "きおく") };

            var result = RubyMarkup.ToRichText("<link=\"link\"><color=red>記憶</color></link>", entries);

            Assert.That(result, Is.EqualTo(
                "<link=\"link\"><color=red>"
                + RubyMarkup.BuildOverlay("記憶", "きおく")
                + "</color></link>"));
            Assert.That(result, Does.Not.Contain("リンク"), "タグ名・属性値は親文字として拾わない");
        }

        [Test]
        public void ToRichText_親文字は渡された順に一致させる()
        {
            // ToRichText は各位置で渡された順に試すだけで最長一致は選ばない。
            // 親文字長の降順で渡すのは呼び出し側の責務 (それを保証する層は RubyDictionary.Load = 下のテスト)。
            // 自動 は 自動人形 の先頭に重なるので、順序を守らないと短い方が先取りしてしまう。
            var entries = new List<RubyEntry> { new RubyEntry("自動人形", "オートマタ"), new RubyEntry("自動", "じどう") };

            var result = RubyMarkup.ToRichText("彼女は自動人形だ", entries);

            Assert.That(result, Is.EqualTo("彼女は" + RubyMarkup.BuildOverlay("自動人形", "オートマタ") + "だ"));
            Assert.That(result, Does.Not.Contain("じどう"), "短い 自動 が 自動人形 を先取りしない");
        }

        [Test]
        public void RubyDictionary_定義順によらず長い親文字を優先する()
        {
            // Load が親文字長の降順へ整列するので、先頭が重なる短い語を先に定義しても長い語が勝つ
            var dictionary = new RubyDictionary();
            dictionary.Load("ruby '自動', 'じどう'\nruby '自動人形', 'オートマタ'\n");

            var result = dictionary.ApplyTo("彼女は自動人形だ");

            Assert.That(result, Is.EqualTo("彼女は" + RubyMarkup.BuildOverlay("自動人形", "オートマタ") + "だ"));
            Assert.That(result, Does.Not.Contain("じどう"), "定義順のままだと 自動 が先取りしてしまう");
        }

        [Test]
        public void ToRichText_付与しない語は親文字だけを出す()
        {
            // 第 3 引数 (FirstOnly) を true にしているのは、ToRichText が FirstOnly を一切見ず
            // shouldRender の結果だけで分岐することを示すため (状態管理は RubyDictionary 側の責務)。
            var entries = new List<RubyEntry> { new RubyEntry("工房", "こうぼう", true) };

            var result = RubyMarkup.ToRichText("古い工房に篭る", entries, _ => false);

            Assert.That(result, Is.EqualTo("古い工房に篭る"), "親文字だけが残り overlay のタグは 1 つも出ない");
        }

        [Test]
        public void RubyDictionary_初出のみの語は一度だけ付きResetShownで復帰する()
        {
            var dictionary = new RubyDictionary();
            dictionary.Load("ruby '工房', 'こうぼう', :first\nruby '記憶', 'きおく'\n");

            var first = dictionary.ApplyTo("工房と記憶");
            Assert.That(first, Is.EqualTo(
                RubyMarkup.BuildOverlay("工房", "こうぼう") + "と" + RubyMarkup.BuildOverlay("記憶", "きおく")));

            var second = dictionary.ApplyTo("工房と記憶");
            Assert.That(second, Is.EqualTo("工房と" + RubyMarkup.BuildOverlay("記憶", "きおく")),
                "初出のみの語は 2 回目以降 親文字だけになり、常時表示の語は毎回付く");

            dictionary.ResetShown();
            Assert.That(dictionary.ApplyTo("工房"), Is.EqualTo(RubyMarkup.BuildOverlay("工房", "こうぼう")),
                "周回時は初出から再開する");
        }
    }
}
