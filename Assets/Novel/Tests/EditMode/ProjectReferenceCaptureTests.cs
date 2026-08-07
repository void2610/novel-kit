#nullable enable
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Cysharp.Threading.Tasks;
using Novel.Assets;
using Novel.Editor;
using Novel.Integration;
using Novel.Runtime;
using NUnit.Framework;
using VContainer;

namespace Novel.Tests
{
    // project-reference ADR: RegisterNovelKitCore の build callback が「実際に配線されたチャンネル」
    // (後勝ち差し替え込み) から音キー/構図の目録をキャプチャする契約を固定する
    public sealed class ProjectReferenceCaptureTests
    {
        private sealed class StubView : INovelView
        {
            public UniTask ShowMessageAsync(NovelLine line, CancellationToken ct) => UniTask.CompletedTask;
            public UniTask<int> ShowChoicesAsync(IReadOnlyList<string> options, CancellationToken ct) => UniTask.FromResult(0);
            public void SetMessageWindowVisible(bool visible) { }
            public void ClearMessage() { }
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

        private sealed class StubSource : IScenarioSource
        {
            public UniTask<byte[]?> LoadBytecodeAsync(string scenarioKey, CancellationToken ct)
                => UniTask.FromResult<byte[]?>(null);
        }

        private sealed class EnumeratingAudioChannel : IAudioChannel
        {
            public UniTask PlaySeAsync(string seKey, CancellationToken ct) => UniTask.CompletedTask;
            public UniTask PlaySeLoopAsync(string seKey, float interval, int count, CancellationToken ct) => UniTask.CompletedTask;
            public void PlayBgm(string bgmKey) { }
            public void StopBgm() { }

            public IEnumerable<AudioKeyInfo> EnumerateKeys()
            {
                yield return new AudioKeyInfo("daily", AudioKeyKind.Bgm, "日常シーン");
                yield return new AudioKeyInfo("door_open", AudioKeyKind.Se);
            }
        }

        private sealed class EnumeratingPortraitChannel : IPortraitChannel
        {
            public UniTask SwitchLayoutAsync(PortraitLayout layout, CancellationToken ct) => UniTask.CompletedTask;
            public UniTask ShowAsync(int slotIndex, ResolvedSprite portrait, CancellationToken ct) => UniTask.CompletedTask;
            public UniTask HideAsync(int slotIndex, CancellationToken ct) => UniTask.CompletedTask;

            public IEnumerable<StageLayoutInfo> EnumerateLayouts()
            {
                yield return new StageLayoutInfo("meeting", 6, "会議室");
            }
        }

        private sealed class EnumeratingCatalog : ICharacterCatalog
        {
            public bool TryGet(string speakerId, out CharacterEntry entry)
            {
                entry = default;
                return false;
            }

            public IEnumerable<CharacterKeyInfo> EnumerateEntries()
            {
                yield return new CharacterKeyInfo("aria", "アリア", "Characters/aria/default");
                yield return new CharacterKeyInfo("noise", "ノイズ");
            }
        }

        private static ContainerBuilder MakeBuilder()
        {
            var builder = new ContainerBuilder();
            builder.RegisterNovelKitCore();
            builder.RegisterInstance<INovelView>(new StubView());
            builder.RegisterInstance<ICharacterCatalog>(new StubCatalog());
            builder.RegisterInstance<IScenarioSource>(new StubSource());
            return builder;
        }

        [Test]
        public void Build時に後勝ち登録したチャンネルの目録をキャプチャする()
        {
            var builder = MakeBuilder();
            builder.RegisterInstance<IAudioChannel>(new EnumeratingAudioChannel());
            builder.RegisterInstance<IPortraitChannel>(new EnumeratingPortraitChannel());

            using var container = builder.Build();

            var snapshot = NovelProjectCapture.Latest;
            Assert.That(snapshot, Is.Not.Null);
            Assert.That(snapshot!.AudioChannelType, Is.EqualTo(nameof(EnumeratingAudioChannel)));
            Assert.That(snapshot.PortraitChannelType, Is.EqualTo(nameof(EnumeratingPortraitChannel)));

            var bgm = snapshot.AudioKeys.Single(k => k.Kind == AudioKeyKind.Bgm);
            Assert.That(bgm.Key, Is.EqualTo("daily"));
            Assert.That(bgm.Note, Is.EqualTo("日常シーン"));
            Assert.That(snapshot.AudioKeys.Single(k => k.Kind == AudioKeyKind.Se).Key, Is.EqualTo("door_open"));

            var layout = snapshot.Layouts.Single();
            Assert.That(layout.Id, Is.EqualTo("meeting"));
            Assert.That(layout.SlotCount, Is.EqualTo(6));
        }

        [Test]
        public void Build時にコード実装カタログのキャラ目録をキャプチャする()
        {
            var builder = new ContainerBuilder();
            builder.RegisterNovelKitCore();
            builder.RegisterInstance<INovelView>(new StubView());
            builder.RegisterInstance<ICharacterCatalog>(new EnumeratingCatalog());
            builder.RegisterInstance<IScenarioSource>(new StubSource());

            using var container = builder.Build();

            var snapshot = NovelProjectCapture.Latest;
            Assert.That(snapshot, Is.Not.Null);
            Assert.That(snapshot!.CharacterCatalogType, Is.EqualTo(nameof(EnumeratingCatalog)));
            var aria = snapshot.Characters.Single(c => c.Id == "aria");
            Assert.That(aria.DisplayName, Is.EqualTo("アリア"));
            Assert.That(aria.DefaultPortraitKey, Is.EqualTo("Characters/aria/default"));
            Assert.That(snapshot.Characters.Single(c => c.Id == "noise").DefaultPortraitKey, Is.Null);
        }

        [Test]
        public void 既定のNull実装は空キーと標準構図をキャプチャする()
        {
            var builder = MakeBuilder();   // IAudioChannel は NullAudioChannel / IPortraitChannel は NullPortraitChannel のまま

            using var container = builder.Build();

            var snapshot = NovelProjectCapture.Latest;
            Assert.That(snapshot, Is.Not.Null);
            Assert.That(snapshot!.AudioKeys, Is.Empty);
            Assert.That(snapshot.Characters, Is.Empty);   // StubCatalog は空目録を明示的に返す
            Assert.That(snapshot.Layouts.Select(l => l.Id),
                Is.EqualTo(new[] { "single", "pair", "trio", "quad", "penta" }));
            Assert.That(snapshot.Layouts.Select(l => l.SlotCount), Is.EqualTo(new[] { 1, 2, 3, 4, 5 }));
        }

        // ---- エディタ側ストアの種別マージ (novel 未配線スコープのビルドがキャプチャ済みの目録を消さない契約) ----
        // なお本ファイルの Build() を含む Edit Mode のコンテナビルドは、ストア側の Play Mode ガードにより永続化されない

        private static NovelProjectCapture.Snapshot Snap(
            AudioKeyInfo[] audio, StageLayoutInfo[] layouts, CharacterKeyInfo[] characters,
            string audioType, string portraitType, string catalogType) =>
            new(audio, layouts, characters, audioType, portraitType, catalogType, System.DateTime.Now);

        private static NovelProjectCapture.Snapshot RealCapture() => Snap(
            new[] { new AudioKeyInfo("daily", AudioKeyKind.Bgm, "日常シーン") },
            new[] { new StageLayoutInfo("meeting", 6, "会議室") },
            new[] { new CharacterKeyInfo("aria", "アリア", "Characters/aria/default") },
            "GameAudioChannel", "GamePortraitChannel", "GameCatalog");

        [Test]
        public void 空の種別は以前のキャプチャを消さない()
        {
            // 既定実装のままのスコープ相当 (音キー空・キャラ空・標準構図)
            var empty = Snap(
                System.Array.Empty<AudioKeyInfo>(), StageLayoutInfo.Defaults.ToArray(),
                System.Array.Empty<CharacterKeyInfo>(),
                "NullAudioChannel", "NullPortraitChannel", "");

            var merged = ProjectReferenceCaptureStore.MergeForTest(RealCapture(), empty);

            Assert.That(merged.AudioKeys.Single().Key, Is.EqualTo("daily"));
            Assert.That(merged.AudioKeys.Single().Note, Is.EqualTo("日常シーン"));
            Assert.That(merged.AudioChannelType, Is.EqualTo("GameAudioChannel"));
            Assert.That(merged.Characters.Single().Id, Is.EqualTo("aria"));
            Assert.That(merged.Characters.Single().DefaultPortraitKey, Is.EqualTo("Characters/aria/default"));
            Assert.That(merged.CharacterCatalogType, Is.EqualTo("GameCatalog"));
            Assert.That(merged.Layouts.Single().Id, Is.EqualTo("meeting"));
            Assert.That(merged.PortraitChannelType, Is.EqualTo("GamePortraitChannel"));
        }

        [Test]
        public void 実データ付きの種別は新しいキャプチャで置き換わる()
        {
            var incoming = Snap(
                new[] { new AudioKeyInfo("battle", AudioKeyKind.Bgm) },
                new[] { new StageLayoutInfo("duo_wide", 2) },
                new[] { new CharacterKeyInfo("noise", "ノイズ") },
                "NewAudioChannel", "NewPortraitChannel", "NewCatalog");

            var merged = ProjectReferenceCaptureStore.MergeForTest(RealCapture(), incoming);

            Assert.That(merged.AudioKeys.Single().Key, Is.EqualTo("battle"));
            Assert.That(merged.AudioChannelType, Is.EqualTo("NewAudioChannel"));
            Assert.That(merged.Characters.Single().Id, Is.EqualTo("noise"));
            Assert.That(merged.Characters.Single().DefaultPortraitKey, Is.Null);
            Assert.That(merged.CharacterCatalogType, Is.EqualTo("NewCatalog"));
            Assert.That(merged.Layouts.Single().Id, Is.EqualTo("duo_wide"));
            Assert.That(merged.PortraitChannelType, Is.EqualTo("NewPortraitChannel"));
        }

        [Test]
        public void 初回キャプチャは標準構図でもそのまま採用する()
        {
            var empty = Snap(
                System.Array.Empty<AudioKeyInfo>(), StageLayoutInfo.Defaults.ToArray(),
                System.Array.Empty<CharacterKeyInfo>(),
                "NullAudioChannel", "NullPortraitChannel", "");

            var merged = ProjectReferenceCaptureStore.MergeForTest(null, empty);

            Assert.That(merged.AudioKeys, Is.Empty);
            Assert.That(merged.Layouts.Select(l => l.Id),
                Is.EqualTo(new[] { "single", "pair", "trio", "quad", "penta" }));
        }
    }
}
