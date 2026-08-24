#nullable enable
using System.Collections.Generic;
using System.IO;
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
        public void Build時に配線されたスプライトローダのrootをキャプチャする()
        {
            var builder = MakeBuilder();
            builder.RegisterInstance<ISpriteLoader>(new ResourcesSpriteLoader("Novel/"));

            using var container = builder.Build();

            var snapshot = NovelProjectCapture.Latest;
            Assert.That(snapshot!.SpriteLoaderType, Is.EqualTo(nameof(ResourcesSpriteLoader)));
            Assert.That(snapshot.SpriteKeyPrefix, Is.EqualTo("Novel/"));
        }

        [Test]
        public void ISpriteKeyPrefixを実装しないローダのrootは不明として扱う()
        {
            using var container = MakeBuilder().Build();   // 既定は NullSpriteLoader (root を名乗らない)

            var snapshot = NovelProjectCapture.Latest;
            Assert.That(snapshot!.SpriteLoaderType, Is.EqualTo(nameof(NullSpriteLoader)));
            Assert.That(snapshot.SpriteKeyPrefix, Is.Null);
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
            string audioType, string portraitType, string catalogType,
            string spriteLoaderType = "", string? spriteKeyPrefix = null) =>
            new(audio, layouts, characters, audioType, portraitType, catalogType, System.DateTime.Now,
                spriteLoaderType, spriteKeyPrefix);

        private static NovelProjectCapture.Snapshot RealCapture() => Snap(
            new[] { new AudioKeyInfo("daily", AudioKeyKind.Bgm, "日常シーン") },
            new[] { new StageLayoutInfo("meeting", 6, "会議室") },
            new[] { new CharacterKeyInfo("aria", "アリア", "Characters/aria/default") },
            "GameAudioChannel", "GamePortraitChannel", "GameCatalog", "ResourcesSpriteLoader", "Novel/");

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

        [Test]
        public void スプライトローダのrootは往復して復元され未キャプチャの配線に消されない()
        {
            // ローダを名乗らない配線 (キャプチャ失敗・別スコープのビルド相当) は root を上書きしない
            var withoutLoader = Snap(
                new[] { new AudioKeyInfo("battle", AudioKeyKind.Bgm) },
                new[] { new StageLayoutInfo("duo_wide", 2) },
                new[] { new CharacterKeyInfo("noise", "ノイズ") },
                "NewAudioChannel", "NewPortraitChannel", "NewCatalog");

            var merged = ProjectReferenceCaptureStore.MergeForTest(RealCapture(), withoutLoader);

            Assert.That(merged.SpriteLoaderType, Is.EqualTo("ResourcesSpriteLoader"));
            Assert.That(merged.SpriteKeyPrefix, Is.EqualTo("Novel/"));
        }

        [Test]
        public void rootを名乗らないローダのプレフィックスはnullのまま保たれる()
        {
            // 空文字 (root 無しが確定) と null (ローダが ISpriteKeyPrefix 未実装で不明) を混同しない
            var unknown = Snap(
                System.Array.Empty<AudioKeyInfo>(), System.Array.Empty<StageLayoutInfo>(),
                System.Array.Empty<CharacterKeyInfo>(),
                "A", "P", "C", "GameSpriteLoader");
            Assert.That(ProjectReferenceCaptureStore.MergeForTest(null, unknown).SpriteKeyPrefix, Is.Null);

            var noRoot = Snap(
                System.Array.Empty<AudioKeyInfo>(), System.Array.Empty<StageLayoutInfo>(),
                System.Array.Empty<CharacterKeyInfo>(),
                "A", "P", "C", "ResourcesSpriteLoader", "");
            Assert.That(ProjectReferenceCaptureStore.MergeForTest(null, noRoot).SpriteKeyPrefix, Is.EqualTo(""));
        }

        // ---- キャプチャ経路 (Publish → ストア)。テスト用シームで一時ファイルへ切り替え、実プロジェクトの
        // Library キャッシュを汚さない ----

        private static readonly string TempCachePath =
            Path.Combine(Path.GetTempPath(), "novelkit-capture-store-test.json");

        [TearDown]
        public void ClearStoreOverrides()
        {
            ProjectReferenceCaptureStore.PlayModeGateForTest = null;
            ProjectReferenceCaptureStore.FilePathForTest = null;
            ProjectReferenceCaptureStore.ResetForTest();
            if (File.Exists(TempCachePath)) File.Delete(TempCachePath);
        }

        private static void UseTempStore(bool playModeGate)
        {
            ProjectReferenceCaptureStore.PlayModeGateForTest = playModeGate;
            ProjectReferenceCaptureStore.FilePathForTest = TempCachePath;
            if (File.Exists(TempCachePath)) File.Delete(TempCachePath);
            ProjectReferenceCaptureStore.ResetForTest();
        }

        [Test]
        public void EditModeのキャプチャはストアに採用されない()
        {
            UseTempStore(playModeGate: false);   // Edit Mode のコンテナビルド相当

            NovelProjectCapture.Publish(RealCapture());

            Assert.That(ProjectReferenceCaptureStore.LoadOrLatest(), Is.Null);
            Assert.That(File.Exists(TempCachePath), Is.False);
        }

        [Test]
        public void PlayMode由来のキャプチャは種別マージで永続化されドメインリロード相当後も残る()
        {
            UseTempStore(playModeGate: true);

            NovelProjectCapture.Publish(RealCapture());
            // novel 未配線スコープのビルド相当 (空キー・空キャラ・標準構図) が後から走っても消えない
            NovelProjectCapture.Publish(Snap(
                System.Array.Empty<AudioKeyInfo>(), StageLayoutInfo.Defaults.ToArray(),
                System.Array.Empty<CharacterKeyInfo>(),
                "NullAudioChannel", "NullPortraitChannel", ""));

            var current = ProjectReferenceCaptureStore.LoadOrLatest();
            Assert.That(current, Is.Not.Null);
            Assert.That(current!.AudioKeys.Single().Key, Is.EqualTo("daily"));
            Assert.That(current.Characters.Single().Id, Is.EqualTo("aria"));
            Assert.That(current.Layouts.Single().Id, Is.EqualTo("meeting"));

            // ドメインリロード相当 (ドメイン内キャッシュ破棄) 後もディスクから復元される
            ProjectReferenceCaptureStore.ResetForTest();
            var reloaded = ProjectReferenceCaptureStore.LoadOrLatest();
            Assert.That(reloaded, Is.Not.Null);
            Assert.That(reloaded!.AudioKeys.Single().Key, Is.EqualTo("daily"));
            Assert.That(reloaded.Characters.Single().Id, Is.EqualTo("aria"));
        }

        [Test]
        public void アセット参照はGUID永続化を往復してアセット実体へ復元される()
        {
            // 本テスト自身の MonoScript を検索で引く (パス直書きは UPM 取り込み時に Packages/ 配下となり解決できない)
            var guid = UnityEditor.AssetDatabase.FindAssets("ProjectReferenceCaptureTests t:MonoScript")[0];
            var asset = UnityEditor.AssetDatabase.LoadMainAssetAtPath(UnityEditor.AssetDatabase.GUIDToAssetPath(guid));
            Assert.That(asset, Is.Not.Null);

            var captured = Snap(
                new[] { new AudioKeyInfo("daily", AudioKeyKind.Bgm, null, asset) },
                System.Array.Empty<StageLayoutInfo>(), System.Array.Empty<CharacterKeyInfo>(),
                "GameAudioChannel", "GamePortraitChannel", "");

            var restored = ProjectReferenceCaptureStore.MergeForTest(null, captured);

            Assert.That(restored.AudioKeys.Single().Asset, Is.SameAs(asset));
        }
    }
}
