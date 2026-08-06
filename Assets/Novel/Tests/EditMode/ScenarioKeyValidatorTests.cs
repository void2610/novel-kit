#nullable enable
using System.Collections.Generic;
using System.Linq;
using Novel.Editor;
using NUnit.Framework;

namespace Novel.Tests
{
    // project-reference ADR: シナリオが使うキーの静的抽出と、正解データとの突き合わせの契約を固定する
    public sealed class ScenarioKeyValidatorTests
    {
        [Test]
        public void 全種類のキー使用箇所を行番号つきで抽出する()
        {
            const string source =
                "chara :alice\n" +                          // 1: Speaker
                "stage :trio, [:alice]\n" +                 // 2: Layout
                "portrait :alice, \"alice/smile\"\n" +      // 3: Speaker + Portrait
                "bg \"room\"\n" +                           // 4: Image
                "still 'ev_sunset'\n" +                     // 5: Image
                "image(\"map\")\n" +                        // 6: Image
                "se \"door\"\n" +                           // 7: Se
                "se_loop \"knock\", 0.5, 3\n" +             // 8: Se
                "bgm \"daily\"\n" +                         // 9: Bgm
                "say \"bob\", \"やあ。\"\n";                  // 10: Speaker

            var usages = ScenarioKeyScanner.Scan(source);

            Assert.That(usages.Select(u => (u.Kind, u.Key, u.Line)), Is.EquivalentTo(new[]
            {
                (ScenarioKeyKind.Speaker, "alice", 1),
                (ScenarioKeyKind.Layout, "trio", 2),
                (ScenarioKeyKind.Speaker, "alice", 3),
                (ScenarioKeyKind.Portrait, "alice/smile", 3),
                (ScenarioKeyKind.Image, "room", 4),
                (ScenarioKeyKind.Image, "ev_sunset", 5),
                (ScenarioKeyKind.Image, "map", 6),
                (ScenarioKeyKind.Se, "door", 7),
                (ScenarioKeyKind.Se, "knock", 8),
                (ScenarioKeyKind.Bgm, "daily", 9),
                (ScenarioKeyKind.Speaker, "bob", 10),
            }));
        }

        [Test]
        public void コメント_空キー_式で組み立てたキーは対象外()
        {
            const string source =
                "# bg \"comment_only\"\n" +
                "bg \"room\"  # se \"in_comment\"\n" +
                "narration \"文中の # は区切りではない\"\n" +
                "bgm \"\"\n" +                       // 停止 (空キー)
                "bg \"room_#{n}\"\n" +               // 実行時に組み立てるキー
                "hide_image\n" +                     // 引数なし命令は image にマッチしない
                "exit_chara :alice\n";               // chara を含む別命令にマッチしない

            var usages = ScenarioKeyScanner.Scan(source);

            Assert.That(usages.Select(u => (u.Kind, u.Key)),
                Is.EquivalentTo(new[] { (ScenarioKeyKind.Image, "room") }));
        }

        [Test]
        public void 正解データに無いキーだけを数え_情報源が無い種別はスキップする()
        {
            const string source =
                "chara :alice\n" +
                "chara :typo_chan\n" +      // 未定義
                "bg \"room\"\n" +
                "bg \"room_typo\"\n" +      // 未定義
                "se \"door\"\n";            // SeKeys = null なのでスキップ

            var known = new ScenarioKeyValidator.KnownKeys
            {
                Speakers = new HashSet<string> { "alice" },
                ImageKeys = new HashSet<string> { "room" },
                SeKeys = null,
                BgmKeys = null,
                Layouts = null,
            };

            Assert.That(ScenarioKeyValidator.Validate("test.rb", source, known), Is.EqualTo(2));
        }
    }
}
