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
    // キー→Sprite の解決が runtime 側で行われ、実装には解決済みスプライトが渡ることを固定する
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

        private sealed class RecordingBackgroundChannel : IBackgroundChannel
        {
            public readonly List<ResolvedSprite> Backgrounds = new();

            public UniTask ShowAsync(ResolvedSprite background, CancellationToken ct)
            {
                Backgrounds.Add(background);
                return UniTask.CompletedTask;
            }
        }

        private sealed class RecordingStillChannel : IStillChannel
        {
            public readonly List<ResolvedSprite> Stills = new();

            public UniTask ShowAsync(ResolvedSprite still, CancellationToken ct)
            {
                Stills.Add(still);
                return UniTask.CompletedTask;
            }
        }

        private readonly List<Object> _created = new();

        // EditMode では生成した Sprite/Texture が leaked objects として警告されるため明示的に破棄する
        [TearDown]
        public void TearDown()
        {
            foreach (var o in _created) Object.DestroyImmediate(o);
            _created.Clear();
        }

        private Sprite MakeSprite()
        {
            var texture = new Texture2D(1, 1);
            var sprite = Sprite.Create(texture, new Rect(0, 0, 1, 1), Vector2.zero);
            _created.Add(texture);
            _created.Add(sprite);
            return sprite;
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

        private static NovelCommandHandler MakeHandler(ISpriteLoader loader, IBackgroundChannel background,
            IStillChannel? still = null) =>
            new(new StubView(), new StubStateStore(), new IdentityTextResolver(), new StubCatalog(),
                background: background, still: still, sprites: loader);

        [Test]
        public void bg_ローダーで解決したスプライトが渡る()
        {
            var sprite = MakeSprite();
            var loader = new RecordingSpriteLoader { Result = sprite };
            var background = new RecordingBackgroundChannel();

            MakeHandler(loader, background).On(new BackgroundCommand { BackgroundKey = "room" }, CancellationToken.None)
                .GetAwaiter().GetResult();

            Assert.That(loader.Requested, Is.EqualTo(new[] { "room" }));
            Assert.That(background.Backgrounds, Has.Count.EqualTo(1));
            Assert.That(background.Backgrounds[0].Sprite, Is.EqualTo(sprite));
            Assert.That(background.Backgrounds[0].Key, Is.EqualTo("room"));
        }

        [Test]
        public void still_はキーも一緒に渡る()
        {
            // スチルは収集要素になるため、どれを表示したかを game が知る必要がある
            var sprite = MakeSprite();
            var loader = new RecordingSpriteLoader { Result = sprite };
            var still = new RecordingStillChannel();

            MakeHandler(loader, new RecordingBackgroundChannel(), still)
                .On(new StillCommand { StillKey = "cg01" }, CancellationToken.None).GetAwaiter().GetResult();

            Assert.That(loader.Requested, Is.EqualTo(new[] { "cg01" }));
            Assert.That(still.Stills, Has.Count.EqualTo(1));
            Assert.That(still.Stills[0].Sprite, Is.EqualTo(sprite));
            Assert.That(still.Stills[0].Key, Is.EqualTo("cg01"));
        }

        [Test]
        public void ロード失敗時もキーは渡る()
        {
            var loader = new RecordingSpriteLoader { Result = null };
            var background = new RecordingBackgroundChannel();

            MakeHandler(loader, background).On(new BackgroundCommand { BackgroundKey = "missing" }, CancellationToken.None)
                .GetAwaiter().GetResult();

            Assert.That(background.Backgrounds, Has.Count.EqualTo(1));
            Assert.That(background.Backgrounds[0].Sprite, Is.Null);
            // 未解決でもキーは渡す (実装が「消去」と「ロード失敗」を区別できるように)
            Assert.That(background.Backgrounds[0].Key, Is.EqualTo("missing"));
        }

        [Test]
        public void 空キーはローダーを呼ばない()
        {
            var loader = new RecordingSpriteLoader { Result = MakeSprite() };
            var background = new RecordingBackgroundChannel();

            MakeHandler(loader, background).On(new BackgroundCommand { BackgroundKey = "" }, CancellationToken.None)
                .GetAwaiter().GetResult();

            // 空キーは消去でロード対象ではない (実装側に空キーガードを強いない)
            Assert.That(loader.Requested, Is.Empty);
            Assert.That(background.Backgrounds, Has.Count.EqualTo(1));
            Assert.That(background.Backgrounds[0].Sprite, Is.Null);
            Assert.That(background.Backgrounds[0].Key, Is.Empty);
        }

        [Test]
        public void ローダー未供給でもキーは渡る()
        {
            var background = new RecordingBackgroundChannel();
            var handler = new NovelCommandHandler(new StubView(), new StubStateStore(),
                new IdentityTextResolver(), new StubCatalog(), background: background);

            handler.On(new BackgroundCommand { BackgroundKey = "room" }, CancellationToken.None).GetAwaiter().GetResult();

            Assert.That(background.Backgrounds, Has.Count.EqualTo(1));
            Assert.That(background.Backgrounds[0].Sprite, Is.Null);
            Assert.That(background.Backgrounds[0].Key, Is.EqualTo("room"));
        }

        [Test]
        public void 既定値のキーは空文字になる()
        {
            // struct なので default や配列要素はコンストラクタを通らない
            Assert.That(default(ResolvedSprite).Key, Is.Empty);
            Assert.That(ResolvedSprite.None.Key, Is.Empty);
            Assert.That(new ResolvedSprite(null!, null).Key, Is.Empty);
        }

        [Test]
        public void 消去とロード失敗はIsClearedで区別できる()
        {
            // どちらも IsLoaded は false なので、実装が両者を同一視しないための述語
            Assert.That(ResolvedSprite.None.IsCleared, Is.True);
            Assert.That(new ResolvedSprite("missing", null).IsCleared, Is.False);
            Assert.That(new ResolvedSprite("missing", null).IsLoaded, Is.False);
        }
    }
}
