#nullable enable
using System.Collections;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using Novel.Editor;
using Novel.Runtime;
using Novel.View;
using NUnit.Framework;
using UnityEngine.TestTools;

namespace Novel.Tests
{
    // project-reference ADR: シナリオのスタブ実行によるキー収集と、正解データとの突き合わせの契約を固定する
    public sealed class ScenarioKeyValidatorTests
    {
        private static UniTask<ScenarioKeyValidator.CollectResult> Collect(string scenarioKey) =>
            ScenarioKeyValidator.CollectAsync(new ScenarioSource(new ResourcesTextAssetLoader()), scenarioKey);

        [UnityTest]
        public IEnumerator 実行で流れたコマンドからキーを種別ごとに記録する() => UniTask.ToCoroutine(async () =>
        {
            var result = await Collect("test_keys_all");

            Assert.That(result.ExecutionError, Is.Null);
            Assert.That(result.Keys, Is.SupersetOf(new[]
            {
                (ScenarioKeyKind.Speaker, "alice"),          // chara 糖衣の say
                (ScenarioKeyKind.Speaker, "bob"),            // stage の cast
                (ScenarioKeyKind.Speaker, "carol"),          // say のシンボル話者指定 (say :carol, ...)
                (ScenarioKeyKind.PortraitKey, "alice/smile"),
                (ScenarioKeyKind.PortraitKey, "carol/wave"), // say の第 3 引数
                (ScenarioKeyKind.Image, "ev_test"),
                (ScenarioKeyKind.Image, "map_test"),
                (ScenarioKeyKind.Se, "knock"),
                (ScenarioKeyKind.Bgm, "daily"),
                (ScenarioKeyKind.Layout, "pair"),
            }));
            // narration (話者 "") はキャラ id として記録しない
            Assert.That(result.Keys, Has.None.Matches<(ScenarioKeyKind Kind, string Key)>(
                k => k.Kind == ScenarioKeyKind.Speaker && k.Key == ""));
        });

        [UnityTest]
        public IEnumerator choose_の回答を変えて再実行し_全分岐のキーを集める() => UniTask.ToCoroutine(async () =>
        {
            var result = await Collect("test_keys_branch");

            Assert.That(result.ExecutionError, Is.Null);
            Assert.That(result.Keys, Is.SupersetOf(new[]
            {
                (ScenarioKeyKind.Image, "bg_a"),   // 回答 0 の分岐
                (ScenarioKeyKind.Image, "bg_b"),   // 回答 1 の分岐
                (ScenarioKeyKind.Se, "click"),
            }));
        });

        [UnityTest]
        public IEnumerator 未登録の独自コマンドがあると完走しない旨を報告する() => UniTask.ToCoroutine(async () =>
        {
            // test_custom_command.rb は cmd :custom_echo を使う (検証ハーネスには語彙が無い)
            var result = await Collect("test_custom_command");

            Assert.That(result.ExecutionError, Is.Not.Null);
        });

        [Test]
        public void Report_は正解データに無いキーだけを数え_情報源の無い種別はスキップする()
        {
            var collected = new ScenarioKeyValidator.CollectResult();
            collected.Keys.Add((ScenarioKeyKind.Speaker, "alice"));
            collected.Keys.Add((ScenarioKeyKind.Speaker, "typo_chan"));  // 未定義
            collected.Keys.Add((ScenarioKeyKind.Image, "room"));
            collected.Keys.Add((ScenarioKeyKind.Image, "room_typo"));    // 未定義
            collected.Keys.Add((ScenarioKeyKind.Se, "door"));            // SeKeys = null なのでスキップ

            var known = new ScenarioKeyValidator.KnownKeys
            {
                Speakers = new HashSet<string> { "alice" },
                ImageKeys = new HashSet<string> { "room" },
                SeKeys = null,
                BgmKeys = null,
                Layouts = null,
            };

            Assert.That(ScenarioKeyValidator.Report("test.rb", null, collected, known), Is.EqualTo(2));
        }
    }
}
