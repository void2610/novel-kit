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
    // キー→Sprite の解決が runtime 側で行われ、View にはスプライトだけが渡ることを固定する
    public class SpriteResolutionTests
    {
        private sealed class RecordingSpriteLoader : ISpriteLoader
        {
            public readonly List<string> Requested = new();
            public Sprite? Result;
            public int ReleaseAllCount;

            public UniTask<Sprite?> LoadAsync(string key, CancellationToken ct)
            {
                Requested.Add(key);
                return UniTask.FromResult(Result);
            }

            public void ReleaseAll() => ReleaseAllCount++;
        }

        private sealed class RecordingBackgroundView : IBackgroundView
        {
            public readonly List<Sprite?> Backgrounds = new();
            public readonly List<Sprite?> Stills = new();

            public UniTask ShowAsync(Sprite? sprite, CancellationToken ct)
            {
                Backgrounds.Add(sprite);
                return UniTask.CompletedTask;
            }

            public UniTask ShowStillAsync(Sprite? sprite, CancellationToken ct)
            {
                Stills.Add(sprite);
                return UniTask.CompletedTask;
            }
        }

        private static Sprite MakeSprite() =>
            Sprite.Create(new Texture2D(1, 1), new Rect(0, 0, 1, 1), Vector2.zero);

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

        private sealed class StubCatalog : ICharacterCatalog
        {
            public bool TryGet(string speakerId, out CharacterEntry entry)
            {
                entry = default;
                return false;
            }
        }

        private static NovelCommandHandler MakeHandler(ISpriteLoader loader, IBackgroundView background) =>
            new(new StubView(), new StubStateStore(), new IdentityTextResolver(), new StubCatalog(),
                background: background, sprites: loader);

        [Test]
        public void bg_ローダーで解決したスプライトがViewへ渡る()
        {
            var sprite = MakeSprite();
            var loader = new RecordingSpriteLoader { Result = sprite };
            var view = new RecordingBackgroundView();

            MakeHandler(loader, view).On(new BackgroundCommand { BackgroundKey = "room" }, CancellationToken.None)
                .GetAwaiter().GetResult();

            Assert.That(loader.Requested, Is.EqualTo(new[] { "room" }));
            Assert.That(view.Backgrounds, Is.EqualTo(new[] { sprite }));
        }

        [Test]
        public void still_ローダーで解決したスプライトがViewへ渡る()
        {
            var sprite = MakeSprite();
            var loader = new RecordingSpriteLoader { Result = sprite };
            var view = new RecordingBackgroundView();

            MakeHandler(loader, view).On(new StillCommand { StillKey = "cg01" }, CancellationToken.None)
                .GetAwaiter().GetResult();

            Assert.That(loader.Requested, Is.EqualTo(new[] { "cg01" }));
            Assert.That(view.Stills, Is.EqualTo(new[] { sprite }));
        }

        [Test]
        public void ロード失敗時はnullがViewへ渡る()
        {
            var loader = new RecordingSpriteLoader { Result = null };
            var view = new RecordingBackgroundView();

            MakeHandler(loader, view).On(new BackgroundCommand { BackgroundKey = "missing" }, CancellationToken.None)
                .GetAwaiter().GetResult();

            Assert.That(view.Backgrounds, Has.Count.EqualTo(1));
            Assert.That(view.Backgrounds[0], Is.Null);
        }

        [Test]
        public void ローダー未供給ならnullがViewへ渡る()
        {
            var view = new RecordingBackgroundView();
            var handler = new NovelCommandHandler(new StubView(), new StubStateStore(),
                new IdentityTextResolver(), new StubCatalog(), background: view);

            handler.On(new BackgroundCommand { BackgroundKey = "room" }, CancellationToken.None).GetAwaiter().GetResult();

            Assert.That(view.Backgrounds, Is.EqualTo(new Sprite?[] { null }));
        }
    }
}
