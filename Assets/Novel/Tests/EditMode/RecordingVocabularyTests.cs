#nullable enable
using System.Linq;
using MRubyCS.Serializer;
using Novel.Runtime;
using NUnit.Framework;
using VitalRouter;

namespace Novel.Tests
{
    [MRubyObject]
    public readonly partial record struct VocabularyProbeCommand : ICommand
    {
        public string SpeakerId { get; init; }
        public float[] Args { get; init; }
        [MRubyMember("as")] public string? DisplayAs { get; init; }
        [MRubyIgnore] public int Internal { get; init; }
    }

    // project-reference ADR: 記録用 vocabulary が MRubyCS.Serializer と同じ規則で Ruby 側の引数名を導くことを固定する
    public sealed class RecordingVocabularyTests
    {
        private sealed class ProbeModule : INovelCommandModule
        {
            public void RegisterVocabulary(INovelVocabulary vocabulary) => vocabulary.Add<VocabularyProbeCommand>("probe");
            public System.IDisposable MapHandlers(ICommandSubscribable router) => new Noop();
            private sealed class Noop : System.IDisposable { public void Dispose() { } }
        }

        [Test]
        public void 語彙とプロパティをRuby側の名前で記録する()
        {
            var recorder = new RecordingVocabulary(nameof(ProbeModule));
            new ProbeModule().RegisterVocabulary(recorder);

            var command = recorder.Commands.Single();
            Assert.That(command.Name, Is.EqualTo("probe"));
            Assert.That(command.CommandType, Is.EqualTo(nameof(VocabularyProbeCommand)));
            Assert.That(command.ModuleType, Is.EqualTo(nameof(ProbeModule)));
            Assert.That(command.Parameters.Select(p => (p.Name, p.TypeName)), Is.EqualTo(new[]
            {
                ("speaker_id", "string"),   // snake_case
                ("args", "float[]"),
                ("as", "string"),           // [MRubyMember] の名前が勝つ (nullable 参照型は "?" を付けない)
                                            // [MRubyIgnore] は載らない
            }));
        }

        [Test]
        public void snake_caseは連続する大文字も1文字ずつ区切る()
        {
            Assert.That(RecordingVocabulary.ToSnakeCase("PortraitKey"), Is.EqualTo("portrait_key"));
            Assert.That(RecordingVocabulary.ToSnakeCase("Key"), Is.EqualTo("key"));
        }
    }
}
