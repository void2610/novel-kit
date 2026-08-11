#nullable enable
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Cysharp.Threading.Tasks;
using Novel.Assets;
using MRubyCS;
using MRubyCS.Serializer;
using Novel.Runtime;
using Novel.View;
using NUnit.Framework;
using UnityEngine.TestTools;
using VitalRouter;
using VitalRouter.MRuby;

namespace Novel.Tests
{
    // 独自コマンドの語彙束縛 + ハンドラ写像を 1 クラスに束ねる拡張口の検証用コマンド/モジュール
    [MRubyObject]
    public readonly partial record struct CustomEchoCommand : ICommand
    {
        public string Text { get; init; }
    }

    [Routes]
    public sealed partial class CustomEchoModule : INovelCommandModule
    {
        public readonly List<string> Received = new();
        public void RegisterVocabulary(MRubyState state) => state.AddCommand<CustomEchoCommand>("custom_echo");
        public IDisposable MapHandlers(ICommandSubscribable router) => MapTo(router);
        public void On(CustomEchoCommand cmd) => Received.Add(cmd.Text);
    }

    // 数値 (float) 引数の独自コマンドが MRuby cmd 経由でハンドラに届くかの再現用。
    // ゲーム側 (apocalyptic-apartment-hunting) で WorldEffectCommand (float[]) と WaitCommand (float) が
    // ランタイムでハンドラまで到達しない症状を観測したため、最小ケースとして float 1 つで切り出した。
    [MRubyObject]
    public readonly partial record struct CustomNumberCommand : ICommand
    {
        public float Value { get; init; }
    }

    [Routes]
    public sealed partial class CustomNumberModule : INovelCommandModule
    {
        public readonly List<float> Received = new();
        public void RegisterVocabulary(MRubyState state) => state.AddCommand<CustomNumberCommand>("custom_number");
        public IDisposable MapHandlers(ICommandSubscribable router) => MapTo(router);
        public void On(CustomNumberCommand cmd) => Received.Add(cmd.Value);
    }

    public sealed class NovelScenarioRunnerTests
    {
        private sealed class FakeView : INovelView
        {
            public readonly List<NovelLine> Lines = new();
            public int ChoiceResult;
            public bool? MessageWindowVisible;

            public UniTask ShowMessageAsync(NovelLine line, CancellationToken ct)
            {
                Lines.Add(line);
                return UniTask.CompletedTask;
            }

            public UniTask<int> ShowChoicesAsync(IReadOnlyList<string> options, CancellationToken ct)
                => UniTask.FromResult(ChoiceResult);

            public void SetMessageWindowVisible(bool visible) => MessageWindowVisible = visible;

            public void ClearMessage() { }
        }

        // 解決したスプライトにキー名を付けて返す fake (どのキーが届いたかを name で検証できるように)
        private sealed class KeyNamedSpriteLoader : ISpriteLoader
        {
            public UniTask<UnityEngine.Sprite?> LoadAsync(string key, CancellationToken ct)
            {
                var sprite = UnityEngine.Sprite.Create(new UnityEngine.Texture2D(1, 1),
                    new UnityEngine.Rect(0, 0, 1, 1), UnityEngine.Vector2.zero);
                sprite.name = key;
                return UniTask.FromResult<UnityEngine.Sprite?>(sprite);
            }

            public void ReleaseAll() { }
        }

        // image / hide_image が ICenterImageChannel へ届くかを記録する fake
        private sealed class FakeCenterImageChannel : ICenterImageChannel
        {
            public readonly List<string> Calls = new();
            public UniTask ShowAsync(ResolvedSprite image, CancellationToken ct)
            {
                Calls.Add("show:" + image.Sprite?.name);
                return UniTask.CompletedTask;
            }
            public UniTask HideAsync(CancellationToken ct)
            {
                Calls.Add("hide");
                return UniTask.CompletedTask;
            }
        }

        // gate 解放までブロックし ct で中断可能な View（switch-latest 検証用）
        private sealed class GatedView : INovelView
        {
            private readonly UniTaskCompletionSource _gate;
            public GatedView(UniTaskCompletionSource gate) => _gate = gate;

            public UniTask ShowMessageAsync(NovelLine line, CancellationToken ct) => _gate.Task.AttachExternalCancellation(ct);
            public UniTask<int> ShowChoicesAsync(IReadOnlyList<string> options, CancellationToken ct) => UniTask.FromResult(0);
            public void SetMessageWindowVisible(bool visible) { }
            public void ClearMessage() { }
        }

        private sealed class EmptyCatalog : ICharacterCatalog
        {
            public bool TryGet(string speakerId, out CharacterEntry entry)
            {
                entry = default;
                return false;
            }

            public IEnumerable<CharacterKeyInfo> EnumerateEntries() => System.Array.Empty<CharacterKeyInfo>();
        }

        // 多言語 resolver の代理: 原文に接頭辞を付けて「別言語のテキスト」を模す
        private sealed class PrefixTextResolver : ITextResolver
        {
            private readonly string _prefix;
            public PrefixTextResolver(string prefix) => _prefix = prefix;
            public string Resolve(string raw) => _prefix + raw;
        }

        private sealed class FakeErrorHandler : INovelErrorHandler
        {
            public bool Called;
            public string? Key;

            public void OnScenarioFaulted(NovelErrorInfo error)
            {
                Called = true;
                Key = error.ScenarioKey;
            }
        }

        private static NovelScenarioRunner NewRunner(INovelView view)
            => new(new ScenarioSource(new ResourcesTextAssetLoader()), new Router(), view,
                new IdentityTextResolver(), new EmptyCatalog(),
                preambleSources: new IPreambleSource[] { new PreambleSource(new ResourcesTextAssetLoader()) });

        [UnityTest]
        public IEnumerator シナリオを実行し_say_が_View_へ順に届く() => UniTask.ToCoroutine(async () =>
        {
            var view = new FakeView();
            var runner = new NovelScenarioRunner(
                new ScenarioSource(new ResourcesTextAssetLoader()),
                new Router(),
                view,
                new IdentityTextResolver(),
                new EmptyCatalog(),
                preambleSources: new IPreambleSource[] { new PreambleSource(new ResourcesTextAssetLoader()) });

            var result = await runner.PlayAsync("test_hello", CancellationToken.None);

            Assert.AreEqual(NovelResult.Completed, result);
            Assert.AreEqual(2, view.Lines.Count);
            Assert.AreEqual("こんにちは", view.Lines[0].Text);
            Assert.AreEqual("alice", view.Lines[0].DisplayName);   // カタログ未登録 → id をそのまま表示名
            Assert.IsNull(view.Lines[1].DisplayName);              // narration はナレーション
        });

        // choose の結果が共有テーブル経由で Ruby の state[:key] に読み戻り分岐が成立することを検証
        [UnityTest]
        public IEnumerator choose_の選択が_Ruby_側の分岐へ反映される() => UniTask.ToCoroutine(async () =>
        {
            var view = new FakeView { ChoiceResult = 1 };   // B を選択
            var runner = new NovelScenarioRunner(
                new ScenarioSource(new ResourcesTextAssetLoader()),
                new Router(),
                view,
                new IdentityTextResolver(),
                new EmptyCatalog(),
                preambleSources: new IPreambleSource[] { new PreambleSource(new ResourcesTextAssetLoader()) });

            var result = await runner.PlayAsync("test_choose", CancellationToken.None);

            Assert.AreEqual(NovelResult.Completed, result);
            Assert.AreEqual(1, view.Lines.Count);
            Assert.AreEqual("Bを選んだ", view.Lines[0].Text);
        });

        // index0 を選ぶケース。else 側に落ちても通る index1 と違い、戻り値が壊れると必ず落ちる (回帰防止)
        [UnityTest]
        public IEnumerator choose_のindex0選択が_Ruby_側の分岐へ反映される() => UniTask.ToCoroutine(async () =>
        {
            var view = new FakeView { ChoiceResult = 0 };   // A を選択
            var runner = NewRunner(view);

            var result = await runner.PlayAsync("test_choose", CancellationToken.None);

            Assert.AreEqual(NovelResult.Completed, result);
            Assert.AreEqual(1, view.Lines.Count);
            Assert.AreEqual("Aを選んだ", view.Lines[0].Text);
        });

        // 同一再生中に C# 側 (flag コマンド) が書いた値を Ruby がその場で読み戻せることを検証 (回帰防止)
        [UnityTest]
        public IEnumerator 同一再生中にflagで書いた値を_Ruby_がその場で読み戻せる() => UniTask.ToCoroutine(async () =>
        {
            var view = new FakeView();
            var runner = NewRunner(view);

            var result = await runner.PlayAsync("test_flag_readback", CancellationToken.None);

            Assert.AreEqual(NovelResult.Completed, result);
            Assert.AreEqual("5", view.Lines[0].Text);
        });

        // flag 設定 → CaptureState で引く → 別 runner へ RestoreState → Ruby の state[:key] が読めることを検証
        [UnityTest]
        public IEnumerator flag_がCaptureStateで引け_RestoreState後に_Ruby_から読める() => UniTask.ToCoroutine(async () =>
        {
            var setRunner = NewRunner(new FakeView());
            var setResult = await setRunner.PlayAsync("test_flag_set", CancellationToken.None);
            Assert.AreEqual(NovelResult.Completed, setResult);

            var snapshot = setRunner.CaptureState();
            Assert.AreEqual(5, snapshot.Values["score"]);   // 引いた snapshot に入っている

            var view = new FakeView();
            var readRunner = NewRunner(view);
            readRunner.RestoreState(snapshot);              // continue: 次の再生前に復元
            var readResult = await readRunner.PlayAsync("test_flag_read", CancellationToken.None);
            Assert.AreEqual(NovelResult.Completed, readResult);
            Assert.AreEqual("5", view.Lines[0].Text);       // 復元後に Ruby が読み戻せた
        });

        // choose 自動キー(__始まり)はセーブ除外、明示キーと flag は永続（回帰防止）
        [UnityTest]
        public IEnumerator choose自動キーはCaptureStateで除外され明示キーとflagは残る() => UniTask.ToCoroutine(async () =>
        {
            var runner = NewRunner(new FakeView { ChoiceResult = 1 });
            var result = await runner.PlayAsync("test_choose_keys", CancellationToken.None);
            Assert.AreEqual(NovelResult.Completed, result);

            var keys = runner.CaptureState().Values.Keys;
            Assert.IsTrue(keys.Contains("picked"));   // 明示キーは永続
            Assert.IsTrue(keys.Contains("kept"));     // flag は永続
            Assert.IsFalse(keys.Any(k => k.StartsWith("__", StringComparison.Ordinal)));   // 自動採番は除外
        });

        // 再生中の再入は前を中断して新シナリオへ差し替える（switch-latest・単一 MRubyState 共有）
        [UnityTest]
        public IEnumerator 再生中の再入PlayAsyncは前を中断して差し替える() => UniTask.ToCoroutine(async () =>
        {
            var gate = new UniTaskCompletionSource();
            var runner = NewRunner(new GatedView(gate));
            var first = runner.PlayAsync("test_hello", CancellationToken.None);   // 最初の say で gate 待ちに入る

            var second = runner.PlayAsync("test_hello", CancellationToken.None);  // 差し替え: first を cancel する
            Assert.AreEqual(NovelResult.Cancelled, await first);                  // 前は中断され Cancelled

            gate.TrySetResult();                                                  // second の gate を解放
            Assert.AreEqual(NovelResult.Completed, await second);                 // 差し替え後が完走する
        });

        // MRuby 実行時例外で Faulted を返し INovelErrorHandler へ委譲することを検証
        [UnityTest]
        public IEnumerator MRuby例外で_Faulted_を返し_ErrorHandler_へ委譲する() => UniTask.ToCoroutine(async () =>
        {
            var handler = new FakeErrorHandler();
            var runner = new NovelScenarioRunner(
                new ScenarioSource(new ResourcesTextAssetLoader()),
                new Router(),
                new FakeView(),
                new IdentityTextResolver(),
                new EmptyCatalog(),
                errorHandler: handler,
                preambleSources: new IPreambleSource[] { new PreambleSource(new ResourcesTextAssetLoader()) });

            var result = await runner.PlayAsync("test_error", CancellationToken.None);

            Assert.AreEqual(NovelResult.Faulted, result);
            Assert.IsTrue(handler.Called);
            Assert.AreEqual("test_error", handler.Key);
        });

        // INovelCommandModule が独自コマンドの語彙束縛とハンドラ写像を差し込めることを検証（拡張口）
        [UnityTest]
        public IEnumerator 独自コマンドモジュールが語彙とハンドラを差し込める() => UniTask.ToCoroutine(async () =>
        {
            var module = new CustomEchoModule();
            var runner = new NovelScenarioRunner(
                new ScenarioSource(new ResourcesTextAssetLoader()),
                new Router(),
                new FakeView(),
                new IdentityTextResolver(),
                new EmptyCatalog(),
                preambleSources: new IPreambleSource[] { new PreambleSource(new ResourcesTextAssetLoader()) },
                commandModules: new INovelCommandModule[] { module });

            var result = await runner.PlayAsync("test_custom_command", CancellationToken.None);

            Assert.AreEqual(NovelResult.Completed, result);
            CollectionAssert.AreEqual(new[] { "echoed" }, module.Received);   // 独自 cmd がハンドラへ届いた
        });

        // image / hide_image が ICenterImageChannel へ順に届くことを検証（補足画像の中央表示）
        [UnityTest]
        public IEnumerator image_と_hide_image_が_CenterImageChannel_へ届く() => UniTask.ToCoroutine(async () =>
        {
            var centerImage = new FakeCenterImageChannel();
            var runner = new NovelScenarioRunner(
                new ScenarioSource(new ResourcesTextAssetLoader()),
                new Router(),
                new FakeView(),
                new IdentityTextResolver(),
                new EmptyCatalog(),
                centerImage: centerImage,
                preambleSources: new IPreambleSource[] { new PreambleSource(new ResourcesTextAssetLoader()) },
                sprites: new KeyNamedSpriteLoader());

            var result = await runner.PlayAsync("test_center_image", CancellationToken.None);

            Assert.AreEqual(NovelResult.Completed, result);
            CollectionAssert.AreEqual(new[] { "show:sketch", "hide" }, centerImage.Calls);
        });

        // 空キー image("") は無効 → ShowAsync を呼ばず no-op (消去は hide_image の責務)
        [UnityTest]
        public IEnumerator 空キーの_image_は_CenterImageChannel_を呼ばない() => UniTask.ToCoroutine(async () =>
        {
            var centerImage = new FakeCenterImageChannel();
            var runner = new NovelScenarioRunner(
                new ScenarioSource(new ResourcesTextAssetLoader()),
                new Router(),
                new FakeView(),
                new IdentityTextResolver(),
                new EmptyCatalog(),
                centerImage: centerImage,
                preambleSources: new IPreambleSource[] { new PreambleSource(new ResourcesTextAssetLoader()) },
                sprites: new KeyNamedSpriteLoader());

            var result = await runner.PlayAsync("test_center_image_empty", CancellationToken.None);

            Assert.AreEqual(NovelResult.Completed, result);
            CollectionAssert.IsEmpty(centerImage.Calls);
        });

        // float 引数つきの独自コマンドが MRuby cmd 経由でハンドラへ届くかの回帰再現。
        // 既存の int FlagCommand と string CustomEchoCommand は通っているが、float (および float[]) は
        // ゲーム側ランタイムでハンドラまで到達しない症状が出ているため最小ケースを置く。
        [UnityTest]
        public IEnumerator float引数の独自コマンドがハンドラへ届く() => UniTask.ToCoroutine(async () =>
        {
            var module = new CustomNumberModule();
            var runner = new NovelScenarioRunner(
                new ScenarioSource(new ResourcesTextAssetLoader()),
                new Router(),
                new FakeView(),
                new IdentityTextResolver(),
                new EmptyCatalog(),
                preambleSources: new IPreambleSource[] { new PreambleSource(new ResourcesTextAssetLoader()) },
                commandModules: new INovelCommandModule[] { module });

            var result = await runner.PlayAsync("test_custom_number", CancellationToken.None);

            Assert.AreEqual(NovelResult.Completed, result);
            CollectionAssert.AreEqual(new[] { 0.5f }, module.Received);   // float cmd がハンドラへ届いた
        });

        // 途中復帰: 目標 say より前は表示せず、目標の say から通常表示に戻る（セリフ単位ロード）
        [UnityTest]
        public IEnumerator 途中復帰は目標sayより前を表示せず目標から通常表示する() => UniTask.ToCoroutine(async () =>
        {
            var view = new FakeView();
            var backlog = new RingBufferBacklog();
            var runner = new NovelScenarioRunner(
                new ScenarioSource(new ResourcesTextAssetLoader()),
                new Router(),
                view,
                new IdentityTextResolver(),
                new EmptyCatalog(),
                preambleSources: new IPreambleSource[] { new PreambleSource(new ResourcesTextAssetLoader()) },
                backlog: backlog);

            var result = await runner.PlayAsync("test_hello", new NovelResumePoint(2), CancellationToken.None);

            Assert.AreEqual(NovelResult.Completed, result);
            Assert.AreEqual(1, view.Lines.Count);                    // 1 行目は早送りで非表示
            Assert.AreEqual("ナレーション", view.Lines[0].Text);     // 2 行目 (保存地点) から表示再開
            Assert.AreEqual(2, backlog.Count);                       // バックログは早送り分も再構築される
            Assert.AreEqual(2, runner.CurrentSayNumber);
        });

        // NovelResumePoint.End は全 say を早送りする（マルチセグメントの過去セグメント再構築用）
        [UnityTest]
        public IEnumerator Endまでの途中復帰は全sayを表示なしで完走する() => UniTask.ToCoroutine(async () =>
        {
            var view = new FakeView();
            var runner = NewRunner(view);

            var result = await runner.PlayAsync("test_hello", NovelResumePoint.End, CancellationToken.None);

            Assert.AreEqual(NovelResult.Completed, result);
            CollectionAssert.IsEmpty(view.Lines);
            Assert.AreEqual(2, runner.CurrentSayNumber);
        });

        // 早送り中の choose は復元済みの明示キーを保ち、未復元の自動採番キーだけ UI に落ちる
        [UnityTest]
        public IEnumerator 早送り中のchooseは復元済みキーの選択を保つ() => UniTask.ToCoroutine(async () =>
        {
            var runner = NewRunner(new FakeView { ChoiceResult = 0 });   // 再選択が起きたら 0 が書かれてしまう
            runner.RestoreState(new NovelStateSnapshot(
                new Dictionary<string, int> { { "picked", 1 } }, Array.Empty<string>()));

            var result = await runner.PlayAsync("test_choose_keys", NovelResumePoint.End, CancellationToken.None);

            Assert.AreEqual(NovelResult.Completed, result);
            Assert.AreEqual(1, runner.CaptureState().Values["picked"]);   // 復元値が再選択で潰れていない
        });

        // 既読 ID は resolve 前の原文基準: 言語 (resolver) を切り替えても既読/スキップが分断しない
        // (localization ADR の先行コア変更。恒等 resolver では従来ハッシュと同一なのでセーブ互換も不変)
        [UnityTest]
        public IEnumerator 既読IDは原文基準でresolver切替後も既読が保たれる() => UniTask.ToCoroutine(async () =>
        {
            // 日本語 (恒等) で一度読む
            var jpRunner = NewRunner(new FakeView());
            var jpResult = await jpRunner.PlayAsync("test_hello", CancellationToken.None);
            Assert.AreEqual(NovelResult.Completed, jpResult);
            var snapshot = jpRunner.CaptureState();

            // 別言語 resolver へ差し替えて同じシナリオを再生（言語切替 + continue 相当）
            var view = new FakeView();
            var enRunner = new NovelScenarioRunner(
                new ScenarioSource(new ResourcesTextAssetLoader()),
                new Router(),
                view,
                new PrefixTextResolver("EN:"),
                new EmptyCatalog(),
                preambleSources: new IPreambleSource[] { new PreambleSource(new ResourcesTextAssetLoader()) });
            enRunner.RestoreState(snapshot);
            var enResult = await enRunner.PlayAsync("test_hello", CancellationToken.None);

            Assert.AreEqual(NovelResult.Completed, enResult);
            Assert.IsTrue(view.Lines[0].IsAlreadyRead);            // 言語が変わっても既読は保持
            Assert.AreEqual("EN:こんにちは", view.Lines[0].Text);       // 表示は差し替え後のテキスト
            Assert.AreEqual("EN:こんにちは", view.Lines[0].PlainText);  // 平文は表示言語基準のまま
        });

        // テキスト変数 %{key} は resolve 後に IStateStore の値で展開され、未定義はそのまま残る
        // (Ruby 補間 #{} と違いテンプレートが C# に届くため、多言語キー照合・既読 ID と両立する)
        [UnityTest]
        public IEnumerator テキスト変数が状態値で展開され未定義は温存される() => UniTask.ToCoroutine(async () =>
        {
            var view = new FakeView();
            var runner = NewRunner(view);

            var result = await runner.PlayAsync("test_variables", CancellationToken.None);

            Assert.AreEqual(NovelResult.Completed, result);
            Assert.AreEqual("所持金は500Gだ", view.Lines[0].Text);         // flag :gold の値が差し込まれる
            Assert.AreEqual("所持金は500Gだ", view.Lines[0].PlainText);    // 平文も展開後基準
            Assert.AreEqual("未定義は%{unknown}のまま", view.Lines[1].Text);  // 未定義は可視のまま (黙って消さない)
        });

        // speaker 宣言は「そのシナリオ限り」の契約。runner を使い回しても次のシナリオへ漏れない
        // (漏れると再生順で表示名が変わる。カタログ未登録 id は id 文字列へフォールバックするのが正)
        [UnityTest]
        public IEnumerator speaker宣言は次のシナリオへ漏れない() => UniTask.ToCoroutine(async () =>
        {
            var view = new FakeView();
            var runner = NewRunner(view);

            var declared = await runner.PlayAsync("test_speaker_declare", CancellationToken.None);
            Assert.AreEqual(NovelResult.Completed, declared);
            Assert.AreEqual("謎の声", view.Lines[0].DisplayName);      // 宣言したシナリオでは表示名が効く

            var undeclared = await runner.PlayAsync("test_speaker_undeclared", CancellationToken.None);
            Assert.AreEqual(NovelResult.Completed, undeclared);
            Assert.AreEqual("ghost", view.Lines[1].DisplayName);        // 未宣言なので id へフォールバック
        });

        // say の表示ごとに IBacklog へ話者・本文（rich）が記録されることを検証
        [UnityTest]
        public IEnumerator say表示ごとにバックログへ話者と本文が積まれる() => UniTask.ToCoroutine(async () =>
        {
            var backlog = new RingBufferBacklog();
            var runner = new NovelScenarioRunner(
                new ScenarioSource(new ResourcesTextAssetLoader()),
                new Router(),
                new FakeView(),
                new IdentityTextResolver(),
                new EmptyCatalog(),
                preambleSources: new IPreambleSource[] { new PreambleSource(new ResourcesTextAssetLoader()) },
                backlog: backlog);

            var result = await runner.PlayAsync("test_hello", CancellationToken.None);

            Assert.AreEqual(NovelResult.Completed, result);
            Assert.AreEqual(2, backlog.Count);
            Assert.AreEqual("alice", backlog.Entries[0].Speaker);
            Assert.AreEqual("こんにちは", backlog.Entries[0].Text);
            Assert.AreEqual("", backlog.Entries[1].Speaker);   // narration は話者なし
        });
    }
}

namespace System.Runtime.CompilerServices
{
    // record struct の init アクセサ用ポリフィル（テストアセンブリは Novel.Commands の内包版を共有しないため）
    internal static class IsExternalInit { }
}
