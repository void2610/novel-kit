#nullable enable
using System.Linq;
using Novel.Editor;
using NUnit.Framework;

namespace Novel.Tests
{
    // project-reference ADR: preamble ソースから糖衣の署名・既定値・説明を読む規則を固定する
    public sealed class RubyDefParserTests
    {
        private const string Source = @"
# novel-kit 共通ヘルパ

# 効果音をループで鳴らす
# (間隔と回数は省略可)
def se_loop(se_key, interval = 0.5, count = 3)
  cmd :se_loop
end

def say(speaker, text = nil, portrait_key = nil, display_as: nil, guest: false)
end

def stage layout_id, cast = nil
end

def world_effect(key, *args)
end

# 真偽判定
def flag?(key)
end

def clear_stage
end

def with_hash(opts = { a: 1, b: [1, 2] })
end
";

        [Test]
        public void defの名前_引数_既定値_直上コメントを読む()
        {
            var defs = RubyDefParser.Parse(Source).ToDictionary(d => d.Name);

            var seLoop = defs["se_loop"];
            Assert.That(seLoop.Comment, Is.EqualTo("効果音をループで鳴らす (間隔と回数は省略可)"), "連続する直上コメントを結合。空行を挟んだ見出しは含めない");
            Assert.That(seLoop.Signature(), Is.EqualTo("se_loop(se_key, interval = 0.5, count = 3)"));
            Assert.That(seLoop.CallTemplate(), Is.EqualTo("se_loop se_key, 0.5, 3"));

            Assert.That(defs["say"].Signature(), Is.EqualTo("say(speaker, text = nil, portrait_key = nil, display_as: nil, guest: false)"));
            Assert.That(defs["say"].CallTemplate(), Is.EqualTo("say speaker, nil, nil, display_as: nil, guest: false"));
            Assert.That(defs["stage"].Signature(), Is.EqualTo("stage(layout_id, cast = nil)"), "括弧なしの def");
            Assert.That(defs["world_effect"].CallTemplate(), Is.EqualTo("world_effect key"), "*args は雛形に出さない");
            Assert.That(defs["flag?"].Name, Is.EqualTo("flag?"));
            Assert.That(defs["flag?"].Comment, Is.EqualTo("真偽判定"));
            Assert.That(defs["clear_stage"].CallTemplate(), Is.EqualTo("clear_stage"));
            Assert.That(defs["with_hash"].Params.Single().Default, Is.EqualTo("{ a: 1, b: [1, 2] }"), "括弧内のカンマで割らない");
        }
    }
}
