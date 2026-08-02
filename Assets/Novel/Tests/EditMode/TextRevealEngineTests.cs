using System.Collections;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using Novel.Runtime;
using NUnit.Framework;
using UnityEngine.TestTools;

namespace Novel.Tests
{
    // 進行エンジンを UI 非依存でヘッドレス検証する（fake clock で時間を駆動）。
    public sealed class TextRevealEngineTests
    {
        // 毎フレーム即完了する fake clock。dt を大きく取れば 1 フレームで全文表示できる
        private sealed class FakeClock : IFrameClock
        {
            public float DeltaTime { get; set; } = 1f;
            public int Frames { get; private set; }

            public UniTask NextFrameAsync(CancellationToken ct)
            {
                Frames++;
                return UniTask.CompletedTask;
            }
        }

        private sealed class FastSettings : INovelPlaybackSettings
        {
            public float CharsPerSecond => 1000f;   // dt=1 で一気に表示
            public float AutoAdvanceDelay => 0f;     // 行末で即進行
            public bool SkipUnread => true;
        }

        // 行末の送り待ちがフレームを消費する設定。RevealOnly との境目を観測するために使う
        private sealed class SlowAdvanceSettings : INovelPlaybackSettings
        {
            public float CharsPerSecond => 1000f;
            public float AutoAdvanceDelay => 3f;
            public bool SkipUnread => true;
        }

        [Test]
        public void Build_タグを除いた可視文字数を返す()
        {
            var engine = new TextRevealEngine(new FastSettings(), new FakeClock());
            var total = engine.Build(NovelTagLexer.Parse("ab<color=#fff>cd</color><w=1>e"));
            Assert.AreEqual(5, total);
        }

        [Test]
        public void Build_shake区間を可視index単位で算出する()
        {
            var engine = new TextRevealEngine(new FastSettings(), new FakeClock());
            engine.Build(NovelTagLexer.Parse("ab<shake>cd</shake>e"));
            Assert.AreEqual(1, engine.ShakeSpans.Count);
            Assert.AreEqual((2, 4), engine.ShakeSpans[0]);
        }

        [UnityTest]
        public IEnumerator RevealAsync_全文を表示して完了する() => UniTask.ToCoroutine(async () =>
        {
            var engine = new TextRevealEngine(new FastSettings(), new FakeClock()) { Auto = true };
            var total = engine.Build(NovelTagLexer.Parse("やあ<shake>世界</shake>"));

            int last = -1;
            await engine.RevealAsync(alreadyRead: false, onVisible: v => last = v, ct: CancellationToken.None);

            Assert.AreEqual(4, total);
            Assert.AreEqual(4, last);   // 最終的に全可視文字が表示された
        });

        [UnityTest]
        public IEnumerator RevealAsync_skip既読行はタイプライタを飛ばす() => UniTask.ToCoroutine(async () =>
        {
            var engine = new TextRevealEngine(new FastSettings(), new FakeClock()) { Skip = true };
            var total = engine.Build(NovelTagLexer.Parse("長い<p>テキスト"));   // <p> があっても skip で素通り

            var values = new List<int>();
            await engine.RevealAsync(alreadyRead: true, onVisible: values.Add, ct: CancellationToken.None);

            Assert.AreEqual(6, total);                       // 長い(2)+テキスト(4)、<p>は数えない
            Assert.AreEqual(6, values[values.Count - 1]);    // 全文表示で完了
        });

        // 打鍵音を打ち終わりで止められること。RevealAsync の完了は送り入力後なのでそこでは止められない
        [UnityTest]
        public IEnumerator RevealOnlyAsync_送り待ちを含めず全文表示で返る() => UniTask.ToCoroutine(async () =>
        {
            var clock = new FakeClock();
            var engine = new TextRevealEngine(new SlowAdvanceSettings(), clock) { Auto = true };
            var total = engine.Build(NovelTagLexer.Parse("やあ世界"));

            var last = -1;
            await engine.RevealOnlyAsync(alreadyRead: false, onVisible: v => last = v, ct: CancellationToken.None);
            var framesAfterReveal = clock.Frames;

            Assert.AreEqual(total, last);   // 全文表示は済んでいる

            await engine.WaitForAdvanceAsync(alreadyRead: false, ct: CancellationToken.None);

            Assert.Greater(clock.Frames, framesAfterReveal, "送り待ちが RevealOnlyAsync に含まれている");
        });
    }
}
