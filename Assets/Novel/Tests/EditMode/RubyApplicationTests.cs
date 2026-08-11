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

            public IEnumerable<CharacterKeyInfo> EnumerateEntries() => System.Array.Empty<CharacterKeyInfo>();
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
        public void 平文にはルビが混入しない()
        {
            var view = new RecordingView();
            var handler = new NovelCommandHandler(view, new StubStateStore(), new IdentityTextResolver(), new StubCatalog(),
                ruby: MakeDictionary());

            handler.On(Say("庭に出る"), CancellationToken.None).GetAwaiter().GetResult();

            // View が Text から平文を再計算するとよみが親文字と連なって残るため、runtime が算出済みの平文を渡す
            Assert.That(view.Lines[0].PlainText, Is.EqualTo("庭に出る"));
            Assert.That(view.Lines[0].PlainText, Does.Not.Contain("にわ"));
        }

        [Test]
        public void 既定値の表示行は空文字を返す()
        {
            // struct なので default や配列要素はコンストラクタを通らない
            Assert.That(default(NovelLine).Text, Is.Empty);
            Assert.That(default(NovelLine).PlainText, Is.Empty);
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
        public void 早送りした行でも初出ルビは消費される()
        {
            // 途中復帰の早送りで初出ルビを消費させ、復帰後の初回プレイで既出の初出ルビが再表示されるのを防ぐ契約
            var dictionary = new RubyDictionary();
            dictionary.Load("ruby '庭', 'にわ', :first");
            var view = new RecordingView();
            var progress = new NovelPlaybackProgress();
            progress.Reset(fastForwardTarget: 2);
            var handler = new NovelCommandHandler(view, new StubStateStore(), new IdentityTextResolver(), new StubCatalog(),
                progress: progress, ruby: dictionary);

            handler.On(Say("庭に出る"), CancellationToken.None).GetAwaiter().GetResult();
            handler.On(Say("庭に戻る"), CancellationToken.None).GetAwaiter().GetResult();

            // 1 行目 (早送りで非表示) が初出を消費済みのため、2 行目 (通常表示) にはルビが付かない
            Assert.That(view.Lines, Has.Count.EqualTo(1));
            Assert.That(view.Lines[0].Text, Does.Not.Contain("にわ"));
        }

        [Test]
        public void 選択肢にも辞書ルビが付く()
        {
            var view = new RecordingChoiceView();
            var handler = new NovelCommandHandler(view, new StubStateStore(), new IdentityTextResolver(), new StubCatalog(),
                ruby: MakeDictionary());

            handler.On(new ChooseCommand { Options = new[] { "庭に出る", "家にいる" }, StateKey = "k" }, CancellationToken.None)
                .GetAwaiter().GetResult();

            Assert.That(view.Options[0], Does.Contain("にわ"));
            Assert.That(view.Options[1], Is.EqualTo("家にいる"));
        }

        private sealed class RecordingChoiceView : INovelView
        {
            public IReadOnlyList<string> Options = System.Array.Empty<string>();

            public UniTask ShowMessageAsync(NovelLine line, CancellationToken ct) => UniTask.CompletedTask;

            public UniTask<int> ShowChoicesAsync(IReadOnlyList<string> options, CancellationToken ct)
            {
                Options = options;
                return UniTask.FromResult(0);
            }

            public void SetMessageWindowVisible(bool visible) { }
            public void ClearMessage() { }
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
