#nullable enable
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Cysharp.Threading.Tasks;
using Novel.Assets;
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
        public void 列挙をオーバーライドしない既定実装は空キーと標準構図をキャプチャする()
        {
            var builder = MakeBuilder();   // IAudioChannel は NullAudioChannel / IPortraitChannel は NullPortraitChannel のまま

            using var container = builder.Build();

            var snapshot = NovelProjectCapture.Latest;
            Assert.That(snapshot, Is.Not.Null);
            Assert.That(snapshot!.AudioKeys, Is.Empty);
            Assert.That(snapshot.Layouts.Select(l => l.Id),
                Is.EqualTo(new[] { "single", "pair", "trio", "quad", "penta" }));
            Assert.That(snapshot.Layouts.Select(l => l.SlotCount), Is.EqualTo(new[] { 1, 2, 3, 4, 5 }));
        }
    }
}
