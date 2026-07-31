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
    // ノベルパートを抜けた先では bg が走らないため、game に追跡させると背景を復元できない
    public class BackgroundStateTests
    {
        private sealed class RecordingBackgroundView : IBackgroundView
        {
            public readonly List<string> Shown = new();

            public UniTask ShowAsync(ResolvedSprite background, CancellationToken ct)
            {
                Shown.Add(background.Key);
                return UniTask.CompletedTask;
            }

            public UniTask ShowStillAsync(ResolvedSprite still, CancellationToken ct) => UniTask.CompletedTask;
        }

        private sealed class StubSpriteLoader : ISpriteLoader
        {
            public bool Fail;
            private readonly List<Object> _created = new();

            public UniTask<Sprite?> LoadAsync(string key, CancellationToken ct)
            {
                if (Fail) return UniTask.FromResult<Sprite?>(null);
                var texture = new Texture2D(1, 1);
                var sprite = Sprite.Create(texture, new Rect(0, 0, 1, 1), Vector2.zero);
                _created.Add(texture);
                _created.Add(sprite);
                return UniTask.FromResult<Sprite?>(sprite);
            }

            public void ReleaseAll()
            {
                foreach (var o in _created) Object.DestroyImmediate(o);
                _created.Clear();
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

        private sealed class StubCatalog : ICharacterCatalog
        {
            public bool TryGet(string speakerId, out CharacterEntry entry)
            {
                entry = default;
                return false;
            }
        }

        private readonly List<StubSpriteLoader> _loaders = new();

        [TearDown]
        public void TearDown()
        {
            foreach (var l in _loaders) l.ReleaseAll();
            _loaders.Clear();
        }

        private (NovelCommandHandler handler, NovelPresentationState presentation) MakeHandler(
            IBackgroundView background, bool failLoad = false)
        {
            var loader = new StubSpriteLoader { Fail = failLoad };
            _loaders.Add(loader);
            var presentation = new NovelPresentationState();
            var handler = new NovelCommandHandler(new StubView(), new StubStateStore(), new IdentityTextResolver(),
                new StubCatalog(), background: background, sprites: loader, presentation: presentation);
            return (handler, presentation);
        }

        [Test]
        public void bgで表示した背景キーをruntimeが保持する()
        {
            var (handler, presentation) = MakeHandler(new RecordingBackgroundView());

            handler.On(new BackgroundCommand { BackgroundKey = "room" }, CancellationToken.None)
                .GetAwaiter().GetResult();

            Assert.That(presentation.BackgroundKey, Is.EqualTo("room"));
        }

        [Test]
        public void ロードに失敗した背景は保持しない()
        {
            // 復元時に出ない絵を指し続けないようにする
            var (handler, presentation) = MakeHandler(new RecordingBackgroundView(), failLoad: true);

            handler.On(new BackgroundCommand { BackgroundKey = "missing" }, CancellationToken.None)
                .GetAwaiter().GetResult();

            Assert.That(presentation.BackgroundKey, Is.Empty);
        }

        [Test]
        public void 空キーの消去で保持もクリアされる()
        {
            var (handler, presentation) = MakeHandler(new RecordingBackgroundView());

            handler.On(new BackgroundCommand { BackgroundKey = "room" }, CancellationToken.None)
                .GetAwaiter().GetResult();
            handler.On(new BackgroundCommand { BackgroundKey = "" }, CancellationToken.None)
                .GetAwaiter().GetResult();

            Assert.That(presentation.BackgroundKey, Is.Empty);
        }

        [Test]
        public void スナップショットは背景キーを往復させる()
        {
            var snapshot = new NovelStateSnapshot(new Dictionary<string, int>(), new[] { "id" }, "room");

            Assert.That(snapshot.BackgroundKey, Is.EqualTo("room"));
            // 既定値でも null にはしない (game が空判定だけで扱えるように)
            Assert.That(new NovelStateSnapshot(new Dictionary<string, int>(), new string[0]).BackgroundKey, Is.Empty);
            Assert.That(default(NovelStateSnapshot).BackgroundKey, Is.Empty);
        }
    }
}
