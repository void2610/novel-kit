#nullable enable
using System.Collections.Generic;
using System.Linq;
using Novel.Editor.Localization;
using NUnit.Framework;

namespace Novel.Tests
{
    // 破壊的なテーブル更新（KeyId 保持リネーム / 分離 / 収斂 / 訳の退避・削除 / deprecated 付与・復活）の検証。
    // ITextTableEditor 抽象のおかげで Unity Localization 非導入でも全経路を通せる
    public sealed class ExtractionApplierTests
    {
        private const string File1 = "Assets/Scenarios/a.rb";
        private const string File2 = "Assets/Scenarios/b.rb";

        private static ExtractionPlan PlanWith(params (string File, string[] Texts)[] files)
        {
            var plan = new ExtractionPlan();
            foreach (var (file, texts) in files) plan.CurrentPerFile[file] = texts.ToList();
            return plan;
        }

        private static RenameOp Rename(string oldText, string newText, RenameKind kind, bool isSplit = false)
            => new() { OldText = oldText, NewText = newText, Kind = kind, File = File1, IsSplit = isSplit };

        [Test]
        public void 誤字修正のリネームは安定IDと訳を保ちfuzzyを付ける()
        {
            var table = new FakeTextTable("en");
            table.AddKey("本気なの？");
            table.SetValue("本気なの？", "en", "Are you serious?");
            table.AddSource("本気なの？", File1, 0);
            var originalId = table.IdOf("本気なの？");

            var plan = PlanWith((File1, new[] { "本気なの…？" }));
            plan.Renames.Add(Rename("本気なの？", "本気なの…？", RenameKind.Fuzzy));
            ExtractionApplier.Apply(plan, table);

            Assert.IsFalse(table.ContainsKey("本気なの？"));                       // 旧キーは消費された
            Assert.AreEqual(originalId, table.IdOf("本気なの…？"));                 // 安定 ID が保たれている
            Assert.AreEqual("Are you serious?", table.GetValue("本気なの…？", "en"));  // 訳が追従した
            Assert.AreEqual(("fuzzy", "本気なの？"), table.FuzzyOf("本気なの…？"));   // 要再確認マーク + 旧原文
            Assert.IsFalse(table.IsDeprecated("本気なの…？"));
            Assert.AreEqual(1, table.SaveCount);
        }

        [Test]
        public void タグのみの変更は訳を保ちタグ移植フラグを付ける()
        {
            var table = new FakeTextTable("en");
            table.AddKey("驚いたな");
            table.SetValue("驚いたな", "en", "How surprising");
            table.AddSource("驚いたな", File1, 0);

            var plan = PlanWith((File1, new[] { "驚いた<w=0.4>な" }));
            plan.Renames.Add(Rename("驚いたな", "驚いた<w=0.4>な", RenameKind.TagOnly));
            ExtractionApplier.Apply(plan, table);

            Assert.AreEqual("How surprising", table.GetValue("驚いた<w=0.4>な", "en"));
            Assert.AreEqual(("tag", "驚いたな"), table.FuzzyOf("驚いた<w=0.4>な"));
        }

        [Test]
        public void リライトは旧訳を退避して未訳化する()
        {
            var table = new FakeTextTable("en", "zh");
            table.AddKey("明日は駅で待ち合わせ");
            table.SetValue("明日は駅で待ち合わせ", "en", "Meet at the station tomorrow");
            table.SetValue("明日は駅で待ち合わせ", "zh", "明天在车站见");
            table.AddSource("明日は駅で待ち合わせ", File1, 0);
            var originalId = table.IdOf("明日は駅で待ち合わせ");

            var plan = PlanWith((File1, new[] { "明日は港に集合しよう" }));
            plan.Renames.Add(Rename("明日は駅で待ち合わせ", "明日は港に集合しよう", RenameKind.Rewritten));
            ExtractionApplier.Apply(plan, table);

            var key = "明日は港に集合しよう";
            Assert.AreEqual(originalId, table.IdOf(key));                 // エントリ自体は同一 (履歴を保つ)
            Assert.IsNull(table.GetValue(key, "en"));                     // stale 訳は表示させない
            Assert.IsNull(table.GetValue(key, "zh"));
            Assert.IsNull(table.FuzzyOf(key));                            // リライトは fuzzy でなく未訳 (ADR)
            var archived = table.ArchivedOf(key);
            Assert.AreEqual(2, archived.Count);                           // 全ロケールの旧訳を参考退避
            CollectionAssert.AreEquivalent(new[] { "en", "zh" }, archived.Select(a => a.Locale));
            Assert.IsTrue(archived.All(a => a.PreviousSource == "明日は駅で待ち合わせ"));   // 退避元は「旧原文」
        }

        [Test]
        public void 分離は旧エントリを残し新エントリへ訳をコピーする()
        {
            // 同一原文が 2 ファイルに出現し、片方だけ変更された (共有行ルール)
            var table = new FakeTextTable("en");
            table.AddKey("はい");
            table.SetValue("はい", "en", "Yes");
            table.AddSource("はい", File1, 0);
            table.AddSource("はい", File2, 0);
            var originalId = table.IdOf("はい");

            var plan = PlanWith((File1, new[] { "はい！" }), (File2, new[] { "はい" }));
            plan.Renames.Add(Rename("はい", "はい！", RenameKind.Fuzzy, isSplit: true));
            ExtractionApplier.Apply(plan, table);

            Assert.AreEqual(originalId, table.IdOf("はい"));               // 他所の出現はそのまま生存
            Assert.AreEqual("Yes", table.GetValue("はい", "en"));
            Assert.IsFalse(table.IsDeprecated("はい"));
            Assert.AreEqual("Yes", table.GetValue("はい！", "en"));          // 変更側は訳をコピーして起票
            Assert.AreEqual(("fuzzy", "はい"), table.FuzzyOf("はい！"));
            CollectionAssert.AreEqual(new[] { File2 }, table.SourcesOf("はい").Select(s => s.SourceFile));
            CollectionAssert.AreEqual(new[] { File1 }, table.SourcesOf("はい！").Select(s => s.SourceFile));
        }

        [Test]
        public void リネーム先が既存キーと衝突したら収斂し既存訳を壊さない()
        {
            var table = new FakeTextTable("en");
            table.AddKey("旧テキスト");
            table.SetValue("旧テキスト", "en", "old translation");
            table.AddSource("旧テキスト", File1, 0);
            table.AddKey("既存テキスト");
            table.SetValue("既存テキスト", "en", "existing translation");
            table.AddSource("既存テキスト", File2, 0);
            var existingId = table.IdOf("既存テキスト");

            var plan = PlanWith((File1, new[] { "既存テキスト" }), (File2, new[] { "既存テキスト" }));
            plan.Renames.Add(Rename("旧テキスト", "既存テキスト", RenameKind.Fuzzy));
            plan.Deprecations.Add("旧テキスト");
            ExtractionApplier.Apply(plan, table);

            Assert.AreEqual(existingId, table.IdOf("既存テキスト"));
            Assert.AreEqual("existing translation", table.GetValue("既存テキスト", "en"));   // 上書きされない
            Assert.IsNull(table.FuzzyOf("既存テキスト"));                                   // 正当な訳に fuzzy も付けない
            Assert.IsTrue(table.ContainsKey("旧テキスト"));                                 // 訳資産は削除しない
            Assert.AreEqual("old translation", table.GetValue("旧テキスト", "en"));
            Assert.IsTrue(table.IsDeprecated("旧テキスト"));                                // 出現を失ったのでマーク
            Assert.AreEqual(2, table.SourcesOf("既存テキスト").Count);                       // 両ファイルの出現が載る
        }

        [Test]
        public void 消滅キーは削除せずdeprecatedマークを付ける()
        {
            var table = new FakeTextTable("en");
            table.AddKey("削除された行");
            table.SetValue("削除された行", "en", "removed line");
            table.AddSource("削除された行", File1, 0);

            var plan = PlanWith((File1, new string[0]));
            plan.Deprecations.Add("削除された行");
            ExtractionApplier.Apply(plan, table);

            Assert.IsTrue(table.ContainsKey("削除された行"));
            Assert.AreEqual("removed line", table.GetValue("削除された行", "en"));   // 訳資産は消さない
            Assert.IsTrue(table.IsDeprecated("削除された行"));
            CollectionAssert.IsEmpty(table.SourcesOf("削除された行"));               // 出所は失う
        }

        [Test]
        public void 復活した原文はdeprecatedが解除される()
        {
            var table = new FakeTextTable("en");
            table.AddKey("戻ってきた行");
            table.SetValue("戻ってきた行", "en", "it is back");
            table.SetDeprecated("戻ってきた行", true);

            var plan = PlanWith((File1, new[] { "戻ってきた行" }));
            ExtractionApplier.Apply(plan, table);

            Assert.IsFalse(table.IsDeprecated("戻ってきた行"));
            Assert.AreEqual("it is back", table.GetValue("戻ってきた行", "en"));   // 訳もそのまま使える
            Assert.AreEqual(1, table.SourcesOf("戻ってきた行").Count);
        }

        [Test]
        public void 追跡実績のない手動エントリはdeprecatedにしない()
        {
            var table = new FakeTextTable("en");
            table.AddKey("UI/手動で足したキー");
            table.SetValue("UI/手動で足したキー", "en", "manual");

            var plan = PlanWith((File1, new[] { "シナリオの行" }));
            plan.Additions.Add("シナリオの行");
            ExtractionApplier.Apply(plan, table);

            Assert.IsFalse(table.IsDeprecated("UI/手動で足したキー"));   // 抽出の管轄外は触らない
            Assert.IsTrue(table.ContainsKey("シナリオの行"));
        }

        [Test]
        public void 出所メタデータは毎回再構築され多重出現も出現ごとに載る()
        {
            var table = new FakeTextTable("en");
            table.AddKey("繰り返す行");
            table.AddSource("繰り返す行", "Assets/Scenarios/old.rb", 7);   // 前回の古い出所

            var plan = PlanWith((File1, new[] { "繰り返す行", "別の行", "繰り返す行" }));
            plan.Additions.Add("別の行");
            ExtractionApplier.Apply(plan, table);

            var sources = table.SourcesOf("繰り返す行");
            Assert.AreEqual(2, sources.Count);                                        // 古い出所は残らない
            CollectionAssert.AreEqual(new[] { 0, 2 }, sources.Select(s => s.Occurrence));
            Assert.IsTrue(sources.All(s => s.SourceFile == File1));
            Assert.AreEqual(1, table.SourcesOf("別の行").Count);
        }

        [Test]
        public void 適用は冪等で二度流しても結果が変わらない()
        {
            var table = new FakeTextTable("en");
            table.AddKey("元の行");
            table.SetValue("元の行", "en", "original");
            table.AddSource("元の行", File1, 0);

            var plan = PlanWith((File1, new[] { "元の行です" }));
            plan.Renames.Add(Rename("元の行", "元の行です", RenameKind.Fuzzy));
            ExtractionApplier.Apply(plan, table);
            var idAfterFirst = table.IdOf("元の行です");

            // 2 回目: リネーム元は既に消えているので新規扱いへ落ち、既存エントリを壊さない
            var plan2 = PlanWith((File1, new[] { "元の行です" }));
            plan2.Renames.Add(Rename("元の行", "元の行です", RenameKind.Fuzzy));
            ExtractionApplier.Apply(plan2, table);

            Assert.AreEqual(idAfterFirst, table.IdOf("元の行です"));
            Assert.AreEqual("original", table.GetValue("元の行です", "en"));
            Assert.AreEqual(1, table.SourcesOf("元の行です").Count);
            Assert.IsFalse(table.IsDeprecated("元の行です"));
        }
    }
}
