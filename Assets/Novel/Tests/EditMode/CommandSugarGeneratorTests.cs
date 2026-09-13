#nullable enable
using System;
using System.Collections.Generic;
using System.Text;
using MRubyCS;
using MRubyCS.Compiler;
using Novel.Editor;
using Novel.Runtime;
using NUnit.Framework;

namespace Novel.Tests
{
    public class CommandSugarGeneratorTests
    {
        private static CommandKeyInfo Command(string name, params (string name, string type)[] parameters) =>
            new(name, "TestCommand", "TestModule",
                Array.ConvertAll(parameters, p => new CommandParameterInfo(p.name, p.type)));

        private static readonly HashSet<string> NoReserved = new(StringComparer.Ordinal);

        [Test]
        public void 位置引数と委譲つきの_defを生成する()
        {
            var result = CommandSugarGenerator.Generate(
                new[] { Command("screen_shake", ("power", "float"), ("absolute", "bool")) }, NoReserved);

            StringAssert.Contains("def screen_shake(power = nil, absolute = nil, **kw)", result.Source);
            StringAssert.Contains("h[:power] = power unless power.nil?", result.Source);
            StringAssert.Contains("cmd :screen_shake, **h", result.Source);
            Assert.That(result.Generated, Is.EqualTo(new[] { "screen_shake" }));
        }

        [Test]
        public void 引数なしは_kw委譲だけの_defになる()
        {
            var result = CommandSugarGenerator.Generate(new[] { Command("ping") }, NoReserved);
            StringAssert.Contains("def ping(**kw)", result.Source);
            StringAssert.Contains("cmd :ping, **kw", result.Source);
        }

        [Test]
        public void 組込との衝突と重複と不正名は生成せず理由を残す()
        {
            var reserved = new HashSet<string>(StringComparer.Ordinal) { "say" };
            var result = CommandSugarGenerator.Generate(new[]
            {
                Command("say", ("text", "string")),
                Command("ok"),
                Command("ok"),
                Command("1bad"),
            }, reserved);

            Assert.That(result.Generated, Is.EqualTo(new[] { "ok" }));
            Assert.That(result.Skipped, Has.Count.EqualTo(3));
            StringAssert.DoesNotContain("def say", result.Source);
        }

        [Test]
        public void 不正な引数名のコマンドは_kw委譲のみに落とす()
        {
            var result = CommandSugarGenerator.Generate(new[] { Command("mark", ("2nd", "int")) }, NoReserved);
            StringAssert.Contains("def mark(**kw)", result.Source);
        }

        [Test]
        public void 説明が_def直上コメントになる()
        {
            var command = new CommandKeyInfo("shake", "ShakeCommand", "M",
                new[] { new CommandParameterInfo("power", "float") }, "画面を揺らす");
            var result = CommandSugarGenerator.Generate(new[] { command }, NoReserved);
            StringAssert.Contains("# 画面を揺らす\ndef shake", result.Source.Replace("\r\n", "\n"));
        }

        [Test]
        public void 生成したソースは実際に位置とキーワードの両方で呼べる()
        {
            var result = CommandSugarGenerator.Generate(
                new[] { Command("screen_shake", ("power", "float"), ("duration", "float")) }, NoReserved);

            var state = MRubyState.Create();
            using var compiler = MRubyCompiler.Create(state);
            byte[] Compile(string source)
            {
                using var code = compiler.CompileToBinaryFormat(Encoding.UTF8.GetBytes(source));
                return code.AsSpan().ToArray();
            }
            // cmd をテスト用スタブに差し替え、糖衣が組んだ props をそのまま返させる
            state.LoadBytecode(Compile("def cmd(name, **props)\n  [name, props]\nend\n"));
            state.LoadBytecode(Compile(result.Source));

            var positional = state.LoadBytecode(Compile("screen_shake(2.5).inspect"));
            var keyword = state.LoadBytecode(Compile("screen_shake(duration: 0.5).inspect"));
            Assert.That(state.Stringify(positional).ToString(), Is.EqualTo("[:screen_shake, {power: 2.5}]"));
            Assert.That(state.Stringify(keyword).ToString(), Is.EqualTo("[:screen_shake, {duration: 0.5}]"));
        }
    }
}
