#nullable enable
using System.Collections.Generic;
using System.Linq;
using Novel.Editor.Localization;
using NUnit.Framework;

namespace Novel.Tests
{
    public sealed class ScenarioTextScannerTests
    {
        private static List<string> ScanTexts(string source)
        {
            var charaSet = new HashSet<string>();
            ScenarioTextScanner.CollectCharaDeclarations(source, charaSet);
            return ScenarioTextScanner.Scan(source, charaSet).Texts.Select(t => t.Text).ToList();
        }

        [Test]
        public void say_narration_chara糖衣_choose_as_を出現順に抽出する()
        {
            var source = string.Join("\n",
                "chara :alice",
                "bg \"room\"",                                     // アセットキーは対象外
                "alice \"やあ\"",
                "say \"謎の声\", \"そこにいるのは誰だ\"",              // 話者 id は対象外・本文のみ
                "narration \"——沈黙が流れた\"",
                "n = choose([\"はい\", \"いいえ\"], key: :ask)",     // key: は対象外
                "alice \"良かった！\", as: \"アリス（笑顔）\"");

            var texts = ScanTexts(source);

            CollectionAssert.AreEqual(new[]
            {
                "やあ",
                "そこにいるのは誰だ",
                "——沈黙が流れた",
                "はい",
                "いいえ",
                "良かった！",
                "アリス（笑顔）",   // as: は表示名として抽出 (ランタイムで Resolve を通る)
            }, texts);
        }

        // guest: true はカタログ外の単発キャラ。未登録 id はそのまま表示名として画面に出るため、
        // 本文だけでなく話者リテラルも翻訳対象になる
        [Test]
        public void guest指定のsayは話者名も抽出する()
        {
            CollectionAssert.AreEqual(new[] { "ラー……テイラー……", "？？？" },
                ScanTexts("say '？？？', 'ラー……テイラー……', guest: true"));
            // guest 無しの話者はカタログ id なので抽出しない
            CollectionAssert.AreEqual(new[] { "こんにちは" },
                ScanTexts("say 'alice', 'こんにちは'"));
            // guest: false / 変数指定は「付いていない」扱い
            CollectionAssert.AreEqual(new[] { "こんにちは" },
                ScanTexts("say 'alice', 'こんにちは', guest: false"));
        }

        [Test]
        public void say単引数はナレーション形として本文を抽出する()
        {
            CollectionAssert.AreEqual(new[] { "ナレーション" }, ScanTexts("say \"ナレーション\""));
        }

        [Test]
        public void sayの第3引数portrait_keyは抽出しない()
        {
            var texts = ScanTexts("chara :kii\nsay 'kii', '正体は伏せたまま', 'kii/default', display_as: '？？？'");
            CollectionAssert.AreEqual(new[] { "正体は伏せたまま", "？？？" }, texts);
        }

        // 位置を文字列リテラルの並びで数えると、シンボル話者では本文の位置がずれて
        // 立ち絵キーを翻訳対象にしてしまう (実引数の並びで数える必要がある)
        [Test]
        public void シンボル話者のsayでも本文だけを抽出する()
        {
            CollectionAssert.AreEqual(new[] { "やあ" },
                ScanTexts("say :carol, \"やあ\", \"carol/wave\""));
            CollectionAssert.AreEqual(new[] { "やあ" },
                ScanTexts("say :carol, \"やあ\""));
            CollectionAssert.AreEqual(new[] { "やあ", "？？？" },
                ScanTexts("say :carol, \"やあ\", \"carol/wave\", display_as: \"？？？\""));
        }

        // 変数・メソッド呼び出しが話者や本文に来ても、リテラルでない引数は拾わない
        [Test]
        public void リテラルでない引数は抽出しない()
        {
            CollectionAssert.IsEmpty(ScanTexts("say speaker_id, message"));
            CollectionAssert.AreEqual(new[] { "本文です" }, ScanTexts("say speaker_id, \"本文です\""));
        }

        [Test]
        public void 補間入りリテラルは抽出せずissueとして報告する()
        {
            var charaSet = new HashSet<string>();
            var result = ScenarioTextScanner.Scan("narration \"(answered=#{val(:answered)})\"", charaSet);

            CollectionAssert.IsEmpty(result.Texts);
            Assert.AreEqual(1, result.Issues.Count);
            Assert.AreEqual(1, result.Issues[0].LineNumber);
        }

        [Test]
        public void 複数行にまたがるchoose配列を継続行として抽出する()
        {
            var texts = ScanTexts("choose([\n  \"選択肢A\",\n  \"選択肢B\",\n])");
            CollectionAssert.AreEqual(new[] { "選択肢A", "選択肢B" }, texts);
        }

        [Test]
        public void コメントとエスケープを正しく扱う()
        {
            var texts = ScanTexts(string.Join("\n",
                "# これはコメント",
                "narration \"シャープ#入り\"   # t:a3f9",            // 行末コメントは無視・文字列内 # は保持
                "narration \"引用\\\"符\\\"と改行\\n\"",
                "narration 'それは\\'秘密\\'だ'"));

            CollectionAssert.AreEqual(new[]
            {
                "シャープ#入り",
                "引用\"符\"と改行\n",
                "それは'秘密'だ",
            }, texts);
        }

        // ランタイム (MRuby) が受け取る文字列とキーが一致しないと訳が永久に引けないため、
        // 二重引用符のエスケープはランタイム準拠で解釈する (レビュー指摘 NovelTextExtraction)
        [Test]
        public void 拡張エスケープをランタイム準拠で解釈する()
        {
            var texts = ScanTexts(string.Join("\n",
                "narration \"復帰\\rとベル\\aとエスケープ\\e\"",
                "narration \"16進\\x41と\\x4a\"",
                "narration \"Unicode\\u3042と\\u{1F600}と\\u{3042 3044}\"",
                "narration \"8進\\101と空白\\s\""));

            CollectionAssert.AreEqual(new[]
            {
                "復帰\rとベル\aとエスケープ\x1b",
                "16進Aと\x4a",
                "Unicodeあと\U0001F600とあい",
                "8進Aと空白 ",
            }, texts);
        }

        [Test]
        public void インラインタグは原文の一部としてそのまま保持する()
        {
            var texts = ScanTexts("chara :alice\nalice \"Text shows<w=0.4> bit by bit. <shake>Surprised</shake>?\"");
            CollectionAssert.AreEqual(new[] { "Text shows<w=0.4> bit by bit. <shake>Surprised</shake>?" }, texts);
        }

        [Test]
        public void 宣言されていないメソッドの文字列は抽出しない()
        {
            // bob は chara 宣言なし → 糖衣と見なさない (se/bgm 等の誤検出防止と同じ扱い)
            CollectionAssert.IsEmpty(ScanTexts("bob \"拾ってはいけない\"\nse \"beep\"\nflag \"answered\", 1"));
        }

        [Test]
        public void preamble定義のcmd直呼びは抽出しない()
        {
            CollectionAssert.IsEmpty(ScanTexts("cmd :say, speaker_id: '', text: 'ライブラリ配管'"));
        }
    }
}
