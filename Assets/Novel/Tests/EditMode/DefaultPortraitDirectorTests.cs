using System.Collections;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using Novel.Runtime;
using NUnit.Framework;
using UnityEngine.TestTools;

namespace Novel.Tests
{
    public sealed class DefaultPortraitDirectorTests
    {
        // View 呼び出しを記録するフェイク。 順序保持で「先に旧 cast 退場 → SwitchLayout → 新 cast 表示」を検証できる
        private sealed class RecordingPortraitView : IPortraitView
        {
            public readonly List<string> Calls = new();
            public UniTask SwitchLayoutAsync(PortraitLayout layout, CancellationToken ct)
            {
                Calls.Add($"switch:{layout.Id}");
                return UniTask.CompletedTask;
            }
            public UniTask ShowAsync(int slotIndex, string character, string portraitKey, CancellationToken ct)
            {
                Calls.Add($"show:{slotIndex}:{character}:{portraitKey}");
                return UniTask.CompletedTask;
            }
            public UniTask HideAsync(int slotIndex, CancellationToken ct)
            {
                Calls.Add($"hide:{slotIndex}");
                return UniTask.CompletedTask;
            }
        }

        // 配列形式 (cast を順番で 0..N-1 に割り当て) で stage 宣言したあと、 portrait 呼び出しが該当 slot に解決される
        [UnityTest]
        public IEnumerator StageArrayCast_でportraitが正しいslotに解決される() => UniTask.ToCoroutine(async () =>
        {
            var view = new RecordingPortraitView();
            var director = new DefaultPortraitDirector(view);

            await director.StageAsync(PortraitLayout.Trio, new[] { "taylor", "kii", "protagonist" }, CancellationToken.None);
            await director.ShowAsync("kii", "worry", CancellationToken.None);

            Assert.Contains("switch:trio", view.Calls);
            Assert.Contains("show:1:kii:worry", view.Calls);
        });

        // hash 形式 (明示 index 指定) でも cast が正しく適用される
        [UnityTest]
        public IEnumerator StageDictCast_でindex明示が反映される() => UniTask.ToCoroutine(async () =>
        {
            var view = new RecordingPortraitView();
            var director = new DefaultPortraitDirector(view);

            var cast = new Dictionary<string, int> { ["taylor"] = 2, ["kii"] = 0, ["protagonist"] = 1 };
            await director.StageAsync(PortraitLayout.Trio, cast, CancellationToken.None);
            await director.ShowAsync("taylor", "smile", CancellationToken.None);

            Assert.Contains("show:2:taylor:smile", view.Calls);
        });

        // IsStaged は stage 宣言 / exit / clear_stage に追従して cast 在籍を返す
        [UnityTest]
        public IEnumerator IsStaged_がcast在籍に追従する() => UniTask.ToCoroutine(async () =>
        {
            var view = new RecordingPortraitView();
            var director = new DefaultPortraitDirector(view);

            Assert.IsFalse(director.IsStaged("taylor"));

            await director.StageAsync(PortraitLayout.Pair, new[] { "taylor", "kii" }, CancellationToken.None);
            Assert.IsTrue(director.IsStaged("taylor"));
            Assert.IsTrue(director.IsStaged("kii"));
            Assert.IsFalse(director.IsStaged("cyan"));

            await director.ExitAsync("kii", CancellationToken.None);
            Assert.IsFalse(director.IsStaged("kii"));

            await director.ClearStageAsync(CancellationToken.None);
            Assert.IsFalse(director.IsStaged("taylor"));
        });

        // Stage 切替時: 旧 cast に居て新 cast にいないキャラは Hide (退場)、 layout は SwitchLayout で切替
        [UnityTest]
        public IEnumerator Stage切替で旧castにのみあるキャラがHideされる() => UniTask.ToCoroutine(async () =>
        {
            var view = new RecordingPortraitView();
            var director = new DefaultPortraitDirector(view);

            await director.StageAsync(PortraitLayout.Trio, new[] { "taylor", "kii", "protagonist" }, CancellationToken.None);
            view.Calls.Clear();
            await director.StageAsync(PortraitLayout.Pair, new[] { "taylor", "kii" }, CancellationToken.None);

            // protagonist は旧 cast の slot 2 にいたので Hide される。 順序 (Hide → SwitchLayout) も担保する
            var hideIdx = view.Calls.IndexOf("hide:2");
            var switchIdx = view.Calls.IndexOf("switch:pair");
            Assert.GreaterOrEqual(hideIdx, 0, "退場キャラの Hide が発火していない");
            Assert.GreaterOrEqual(switchIdx, 0, "SwitchLayout が発火していない");
            Assert.Less(hideIdx, switchIdx, "Hide は SwitchLayout より前に呼ばれるべき");
        });

        // 未宣言キャラの portrait は slot 0 にフォールバック (警告ログは想定挙動なので吸収)
        [UnityTest]
        public IEnumerator 未宣言キャラのportraitはslot0にフォールバック() => UniTask.ToCoroutine(async () =>
        {
            var view = new RecordingPortraitView();
            var director = new DefaultPortraitDirector(view);

            await director.StageAsync(PortraitLayout.Pair, new[] { "taylor", "kii" }, CancellationToken.None);
            LogAssert.Expect(UnityEngine.LogType.Warning, new System.Text.RegularExpressions.Regex("stranger.*stage cast"));
            await director.ShowAsync("stranger", "neutral", CancellationToken.None);

            Assert.Contains("show:0:stranger:neutral", view.Calls);
        });

        // Stage 未宣言で portrait が呼ばれたら暗黙 single レイアウト + SwitchLayout を View に通知 + slot 0 で表示
        [UnityTest]
        public IEnumerator Stage未宣言でportraitが呼ばれると暗黙singleにフォールバック() => UniTask.ToCoroutine(async () =>
        {
            var view = new RecordingPortraitView();
            var director = new DefaultPortraitDirector(view);

            LogAssert.Expect(UnityEngine.LogType.Warning, new System.Text.RegularExpressions.Regex("stage 宣言なし"));
            LogAssert.Expect(UnityEngine.LogType.Warning, new System.Text.RegularExpressions.Regex("stage cast"));
            await director.ShowAsync("taylor", "smile", CancellationToken.None);

            // View にも SwitchLayout(single) が伝わってから ShowAsync が走ることを順序込みで担保 (single の slot が未確保のまま Show される不整合を防ぐ)
            var switchIdx = view.Calls.IndexOf("switch:single");
            var showIdx = view.Calls.IndexOf("show:0:taylor:smile");
            Assert.GreaterOrEqual(switchIdx, 0, "SwitchLayout(single) が発火していない");
            Assert.GreaterOrEqual(showIdx, 0, "ShowAsync が発火していない");
            Assert.Less(switchIdx, showIdx, "SwitchLayout は ShowAsync より前に呼ばれるべき");
        });

        // 残留キャラの slot index が Stage 切替で変わった場合: 旧 slot を Hide してから SwitchLayout、 そのあと新 slot で再 Show する
        // (View 側に重複表示が残らないことの回帰防止)
        [UnityTest]
        public IEnumerator Stage切替で残留キャラのslot変更時に旧slotHideと新slot再Showが走る() => UniTask.ToCoroutine(async () =>
        {
            var view = new RecordingPortraitView();
            var director = new DefaultPortraitDirector(view);

            await director.StageAsync(PortraitLayout.Pair, new[] { "taylor", "kii" }, CancellationToken.None);
            await director.ShowAsync("taylor", "smile", CancellationToken.None);
            view.Calls.Clear();

            // taylor (0→1) / kii (1→0) を入替える
            var newCast = new System.Collections.Generic.Dictionary<string, int> { ["taylor"] = 1, ["kii"] = 0 };
            await director.StageAsync(PortraitLayout.Pair, newCast, CancellationToken.None);

            // taylor は旧 slot 0 を Hide してから SwitchLayout 後に新 slot 1 で smile を再 Show
            var hideIdx = view.Calls.IndexOf("hide:0");
            var switchIdx = view.Calls.IndexOf("switch:pair");
            var reshowIdx = view.Calls.IndexOf("show:1:taylor:smile");
            Assert.GreaterOrEqual(hideIdx, 0, "旧 slot Hide が発火していない");
            Assert.GreaterOrEqual(switchIdx, 0, "SwitchLayout が発火していない");
            Assert.GreaterOrEqual(reshowIdx, 0, "新 slot での再 Show が発火していない");
            Assert.Less(hideIdx, switchIdx, "Hide は SwitchLayout より前");
            Assert.Less(switchIdx, reshowIdx, "再 Show は SwitchLayout より後");
        });

        // Exit でキャラが cast から外れ、 該当 slot が Hide される
        [UnityTest]
        public IEnumerator Exit指定キャラがHideされcastから外れる() => UniTask.ToCoroutine(async () =>
        {
            var view = new RecordingPortraitView();
            var director = new DefaultPortraitDirector(view);

            await director.StageAsync(PortraitLayout.Trio, new[] { "taylor", "kii", "protagonist" }, CancellationToken.None);
            view.Calls.Clear();
            await director.ExitAsync("kii", CancellationToken.None);

            Assert.Contains("hide:1", view.Calls);
            Assert.IsFalse(director.CurrentCast.ContainsKey("kii"));
        });

        // ClearStage で全 cast が Hide され空になる (layout はリセットされない)
        [UnityTest]
        public IEnumerator ClearStageで全castがHideされ空になる() => UniTask.ToCoroutine(async () =>
        {
            var view = new RecordingPortraitView();
            var director = new DefaultPortraitDirector(view);

            await director.StageAsync(PortraitLayout.Pair, new[] { "taylor", "kii" }, CancellationToken.None);
            view.Calls.Clear();
            await director.ClearStageAsync(CancellationToken.None);

            Assert.Contains("hide:0", view.Calls);
            Assert.Contains("hide:1", view.Calls);
            Assert.AreEqual(0, director.CurrentCast.Count);
            // layout は維持される (突然真っさらにならないように)
            Assert.AreEqual("pair", director.CurrentLayout.Id);
        });
    }
}
