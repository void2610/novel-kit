#nullable enable
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using Novel.Commands;
using Novel.Runtime;

namespace Novel.Tests
{
    // 辞書ルビは表示専用: NovelLine.Text にだけ付き、既読 ID とバックログには混入しない契約を固定する
    public class RubyApplicationTests
    {
        private sealed class RecordingView : INovelView
        {
            public readonly List<NovelLine> Lines = new();

            public UniTask ShowMessageAsync(NovelLine line, CancellationToken ct)
            {
                Lines.Add(line);
                return UniTask.CompletedTask;
            }

            public UniTask<int> ShowChoicesAsync(IReadOnlyList<string> options, CancellationToken ct) => UniTask.FromResult(0);
            public void SetMessageWindowVisible(bool visible) { }
            public void ClearMessage() { }
        }

        private sealed class StubStateStore : IStateStore
        {
            public readonly List<string> MarkedRead = new();
            private readonly Dictionary<string, int> _values = new();

            public int Get(string key) => _values.TryGetValue(key, out var v) ? v : 0;
            public void Set(string key, int value) => _values[key] = value;
            public void Unset(string key) => _values.Remove(key);
            public bool Has(string key) => _values.ContainsKey(key);
            public bool IsRead(string textId) => false;
            public void MarkRead(string textId) => MarkedRead.Add(textId);
        }

        private sealed class StubCatalog : ICharacterCatalog
        {
            public bool TryGet(string speakerId, out CharacterEntry entry)
            {
                entry = default;
                return false;
            }
        }

        private static SayCommand Say(string text) => new() { SpeakerId = "", Text = text };

        private static RubyDictionary MakeDictionary()
        {
            var dictionary = new RubyDictionary();
            dictionary.Load("ruby '庭', 'にわ'");
            return dictionary;
        }

        [Test]
        public void 表示テキストには辞書ルビが付く()
        {
            var view = new RecordingView();
            var handler = new NovelCommandHandler(view, new StubStateStore(), new IdentityTextResolver(), new StubCatalog(),
                ruby: MakeDictionary());

            handler.On(Say("庭に出る"), CancellationToken.None).GetAwaiter().GetResult();

            Assert.That(view.Lines[0].Text, Does.Contain("にわ"));
        }

        [Test]
        public void 既読IDにはルビが混入しない()
        {
            var store = new StubStateStore();
            var withRuby = new NovelCommandHandler(new RecordingView(), store, new IdentityTextResolver(), new StubCatalog(),
                ruby: MakeDictionary());
            var storeNoRuby = new StubStateStore();
            var withoutRuby = new NovelCommandHandler(new RecordingView(), storeNoRuby, new IdentityTextResolver(), new StubCatalog());

            withRuby.On(Say("庭に出る"), CancellationToken.None).GetAwaiter().GetResult();
            withoutRuby.On(Say("庭に出る"), CancellationToken.None).GetAwaiter().GetResult();

            Assert.That(store.MarkedRead, Is.EqualTo(storeNoRuby.MarkedRead));
        }

        [Test]
        public void バックログにはルビが混入しない()
        {
            var backlog = new RingBufferBacklog();
            var handler = new NovelCommandHandler(new RecordingView(), new StubStateStore(), new IdentityTextResolver(), new StubCatalog(),
                backlog: backlog, ruby: MakeDictionary());

            handler.On(Say("庭に出る"), CancellationToken.None).GetAwaiter().GetResult();

            Assert.That(backlog.Entries, Has.Count.EqualTo(1));
            Assert.That(backlog.Entries[0].Text, Is.EqualTo("庭に出る"));
        }

        [Test]
        public void 辞書未供給なら本文はそのまま表示される()
        {
            var view = new RecordingView();
            var handler = new NovelCommandHandler(view, new StubStateStore(), new IdentityTextResolver(), new StubCatalog());

            handler.On(Say("庭に出る"), CancellationToken.None).GetAwaiter().GetResult();

            Assert.That(view.Lines[0].Text, Is.EqualTo("庭に出る"));
        }
    }
}
