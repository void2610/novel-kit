#nullable enable
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using Novel.Assets;
using Novel.Commands;
using Novel.Runtime;
using UnityEngine;

namespace Novel.Tests
{
    // say のたびに話者の既定立ち絵を出す解決が runtime 側で行われることを固定する
    public class DefaultPortraitTests
    {
        private sealed class RecordingPortraitDirector : IPortraitDirector
        {
            public readonly List<string> Shown = new();
            public readonly HashSet<string> Staged = new();

            public UniTask StageAsync(PortraitLayout layout, IReadOnlyList<string> cast, CancellationToken ct) => UniTask.CompletedTask;
            public UniTask StageAsync(PortraitLayout layout, IReadOnlyDictionary<string, int> cast, CancellationToken ct) => UniTask.CompletedTask;
            public bool IsStaged(string character) => Staged.Contains(character);

            public UniTask ShowAsync(string character, ResolvedSprite portrait, CancellationToken ct)
            {
                Shown.Add($"{character}:{portrait.Key}");
                return UniTask.CompletedTask;
            }

            public UniTask ExitAsync(string character, CancellationToken ct) => UniTask.CompletedTask;
            public UniTask ClearStageAsync(CancellationToken ct) => UniTask.CompletedTask;
        }

        private sealed class StubCatalog : ICharacterCatalog
        {
            public bool TryGet(string speakerId, out CharacterEntry entry)
            {
                if (speakerId == "kii")
                {
                    entry = new CharacterEntry("キイ", "Characters/kii/default");
                    return true;
                }
                entry = default;
                return false;
            }
        }

        private sealed class StubView : INovelView
        {
            public UniTask ShowMessageAsync(NovelLine line, CancellationToken ct) => UniTask.CompletedTask;
            public UniTask<int> ShowChoicesAsync(IReadOnlyList<string> options, CancellationToken ct) => UniTask.FromResult(0);
            public void SetMessageWindowVisible(bool visible) { }
            public void ClearMessage() { }
        }

        private sealed class StubStateStore : IStateStore
        {
            private readonly Dictionary<string, int> _values = new();
            private readonly HashSet<string> _read = new();

            public int Get(string key) => _values.TryGetValue(key, out var v) ? v : 0;
            public void Set(string key, int value) => _values[key] = value;
            public void Unset(string key) => _values.Remove(key);
            public bool Has(string key) => _values.ContainsKey(key);
            public bool IsRead(string textId) => _read.Contains(textId);
            public void MarkRead(string textId) => _read.Add(textId);
        }

        private sealed class NullSprites : ISpriteLoader
        {
            public UniTask<Sprite?> LoadAsync(string key, CancellationToken ct) => UniTask.FromResult<Sprite?>(null);
            public void ReleaseAll() { }
        }

        private static NovelCommandHandler MakeHandler(IPortraitDirector director) =>
            new(new StubView(), new StubStateStore(), new IdentityTextResolver(), new StubCatalog(),
                portraitDirector: director, sprites: new NullSprites());

        private static SayCommand Say(string speakerId, string? portraitKey = null) =>
            new() { SpeakerId = speakerId, Text = "こんにちは", PortraitKey = portraitKey };

        [Test]
        public void stage_cast在籍の話者は既定立ち絵が出る()
        {
            var director = new RecordingPortraitDirector();
            director.Staged.Add("kii");

            MakeHandler(director).On(Say("kii"), CancellationToken.None).GetAwaiter().GetResult();

            Assert.That(director.Shown, Is.EqualTo(new[] { "kii:Characters/kii/default" }));
        }

        [Test]
        public void cast外の話者には既定立ち絵を出さない()
        {
            // clear_stage 後の回想・夢シーン等で、居ないはずのキャラが喋るたびに現れるのを防ぐ
            var director = new RecordingPortraitDirector();

            MakeHandler(director).On(Say("kii"), CancellationToken.None).GetAwaiter().GetResult();

            Assert.That(director.Shown, Is.Empty);
        }

        [Test]
        public void portrait明示指定は既定立ち絵より優先される()
        {
            var director = new RecordingPortraitDirector();
            director.Staged.Add("kii");

            MakeHandler(director).On(Say("kii", "Characters/kii/smile"), CancellationToken.None).GetAwaiter().GetResult();

            Assert.That(director.Shown, Is.EqualTo(new[] { "kii:Characters/kii/smile" }));
        }

        [Test]
        public void カタログ未登録の話者には既定立ち絵を出さない()
        {
            var director = new RecordingPortraitDirector();
            director.Staged.Add("stranger");

            MakeHandler(director).On(Say("stranger"), CancellationToken.None).GetAwaiter().GetResult();

            Assert.That(director.Shown, Is.Empty);
        }

        [Test]
        public void ナレーションには既定立ち絵を出さない()
        {
            var director = new RecordingPortraitDirector();

            MakeHandler(director).On(Say(""), CancellationToken.None).GetAwaiter().GetResult();

            Assert.That(director.Shown, Is.Empty);
        }
    }
}
