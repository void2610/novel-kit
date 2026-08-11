#nullable enable
using System.IO;
using System.Linq;
using Novel.Editor.Localization;
using NUnit.Framework;

namespace Novel.Tests
{
    // 計画立案（前回の出所メタデータ + 今回の走査結果 → リネーム/分離/新規/消滅）の検証
    public sealed class ExtractionPlannerTests
    {
        private const string File1 = "Assets/Scenarios/a.rb";
        private const string File2 = "Assets/Scenarios/b.rb";

        private static ExtractionPlan PlanWith(params (string File, string[] Texts)[] files)
        {
            var plan = new ExtractionPlan();
            foreach (var (file, texts) in files) plan.CurrentPerFile[file] = texts.ToList();
            return plan;
        }

        private static void Track(FakeTextTable table, string key, string file, int occurrence)
        {
            if (!table.ContainsKey(key)) table.AddKey(key);
            table.AddSource(key, file, occurrence);
        }

        [Test]
        public void 誤字修正はリネームとして計画され消滅には落ちない()
        {
            var table = new FakeTextTable("en");
            Track(table, "本気で言っているの？", File1, 0);

            var plan = PlanWith((File1, new[] { "本気で言っているの…？" }));
            ExtractionPlanner.BuildDiff(plan, table);

            Assert.AreEqual(1, plan.Renames.Count);
            Assert.AreEqual(RenameKind.Fuzzy, plan.Renames[0].Kind);
            Assert.IsFalse(plan.Renames[0].IsSplit);
            CollectionAssert.IsEmpty(plan.Deprecations);   // 旧キーはリネームで消費される
            CollectionAssert.IsEmpty(plan.Additions);
        }

        [Test]
        public void 新規行と削除行が正しく分類される()
        {
            var table = new FakeTextTable("en");
            Track(table, "残る行です", File1, 0);
            Track(table, "消える行です", File1, 1);

            var plan = PlanWith((File1, new[] { "残る行です", "全く無関係な新しい0123456789" }));
            ExtractionPlanner.BuildDiff(plan, table);

            CollectionAssert.AreEqual(new[] { "全く無関係な新しい0123456789" }, plan.Additions);
            CollectionAssert.AreEqual(new[] { "消える行です" }, plan.Deprecations);
        }

        [Test]
        public void 多重出現の全出現が変更されると全て分離になり旧原文は消滅へ落ちる()
        {
            // 旧エントリは Apply でリネームされず残るため、出所を失う以上 deprecated 対象でなければならない
            var table = new FakeTextTable("en");
            Track(table, "共通の行", File1, 0);
            Track(table, "共通の行", File2, 0);

            var plan = PlanWith((File1, new[] { "共通の行A" }), (File2, new[] { "共通の行B" }));
            ExtractionPlanner.BuildDiff(plan, table);

            Assert.AreEqual(2, plan.Renames.Count);
            Assert.IsTrue(plan.Renames.All(r => r.IsSplit));
            CollectionAssert.Contains(plan.Deprecations, "共通の行");
        }

        [Test]
        public void 同一キーへの複数リネームは先着だけ消費され後続は消滅レポートに出る()
        {
            // Apply でリネームされるのは先着 1 件だけ。残りは旧キーが残るのでレポートに現れる必要がある
            var table = new FakeTextTable("en");
            Track(table, "統合される行1", File1, 0);
            Track(table, "統合される行2", File1, 1);

            var plan = PlanWith((File1, new[] { "統合された行", "統合された行" }));
            ExtractionPlanner.BuildDiff(plan, table);

            Assert.AreEqual(2, plan.Renames.Count);
            Assert.IsTrue(plan.Renames.All(r => r.NewText == "統合された行"));
            Assert.AreEqual(1, plan.Deprecations.Count);                    // 後続 1 件が残留する
            CollectionAssert.Contains(new[] { "統合される行1", "統合される行2" }, plan.Deprecations[0]);
        }

        [Test]
        public void 変更後の原文が既存キーと同じなら旧キーは消滅へ落ちる()
        {
            var table = new FakeTextTable("en");
            Track(table, "旧テキストです", File1, 0);
            Track(table, "既存テキストです", File2, 0);

            // File1 の行が File2 と同文へ書き換わった (収斂 → 旧エントリは残る)
            var plan = PlanWith((File1, new[] { "既存テキストです" }), (File2, new[] { "既存テキストです" }));
            ExtractionPlanner.BuildDiff(plan, table);

            CollectionAssert.Contains(plan.Deprecations, "旧テキストです");
        }

        [Test]
        public void 計画と適用を通すと誤字修正で訳が追従する()
        {
            // planner → applier の結合。ライターが 1 行直したときの実際の流れ
            var table = new FakeTextTable("en");
            Track(table, "海はどこまでも青かった", File1, 0);
            table.SetValue("海はどこまでも青かった", "en", "The sea was endlessly blue");
            var originalId = table.IdOf("海はどこまでも青かった");

            var plan = PlanWith((File1, new[] { "海はどこまでも青かったんだ" }));
            ExtractionPlanner.BuildDiff(plan, table);
            ExtractionApplier.Apply(plan, table);

            Assert.AreEqual(originalId, table.IdOf("海はどこまでも青かったんだ"));
            Assert.AreEqual("The sea was endlessly blue", table.GetValue("海はどこまでも青かったんだ", "en"));
            Assert.AreEqual("fuzzy", table.FuzzyOf("海はどこまでも青かったんだ")?.Reason);
        }

        [Test]
        public void スキャンはTestsとEditorとチルダフォルダとpreambleを除外する()
        {
            var root = Path.Combine(Path.GetTempPath(), "novelkit_scan_" + Path.GetRandomFileName());
            try
            {
                Write(root, "Scenarios/main.rb", "narration \"本編の行\"");
                Write(root, "Tests/EditMode/Resources/Scenarios/fixture.rb", "narration \"テスト用の行\"");
                Write(root, "Editor/tool.rb", "narration \"ツールの行\"");
                Write(root, "Samples~/Basic/sample.rb", "narration \"サンプルの行\"");
                Write(root, "Scenarios/preamble.rb", "narration \"糖衣定義\"");

                var plan = ExtractionPlanner.Scan(root);

                CollectionAssert.AreEqual(new[] { "本編の行" }, plan.AllCurrentTexts.ToArray());
            }
            finally
            {
                if (Directory.Exists(root)) Directory.Delete(root, true);
            }
        }

        // 除外判定は「ルートからの相対パス」だけで行う。絶対パス全体を見ると、プロジェクトが
        // Editor/ や末尾 ~ のディレクトリ配下に置かれているだけで全ファイルが除外され、
        // 走査 0 件 → 全エントリ deprecated 提案という事故になる
        [Test]
        public void スキャンルート自体がTestsやEditorという名前でも除外されない()
        {
            var root = Path.Combine(Path.GetTempPath(), "novelkit_scan_" + Path.GetRandomFileName());
            var nested = Path.Combine(root, "Editor", "work~", "Tests");   // ルート側にだけ除外語を含める
            try
            {
                Write(nested, "Scenarios/main.rb", "narration \"本編の行\"");
                Write(nested, "Scenarios/Tests/fixture.rb", "narration \"テスト用の行\"");

                var plan = ExtractionPlanner.Scan(nested);

                CollectionAssert.AreEqual(new[] { "本編の行" }, plan.AllCurrentTexts.ToArray());
            }
            finally
            {
                if (Directory.Exists(root)) Directory.Delete(root, true);
            }
        }

        [Test]
        public void 走査0件のときは適用前に気付けるようissueを出す()
        {
            var root = Path.Combine(Path.GetTempPath(), "novelkit_scan_" + Path.GetRandomFileName());
            Directory.CreateDirectory(root);
            try
            {
                var plan = ExtractionPlanner.Scan(root);

                CollectionAssert.IsEmpty(plan.AllCurrentTexts.ToArray());
                Assert.AreEqual(1, plan.Issues.Count);
            }
            finally
            {
                if (Directory.Exists(root)) Directory.Delete(root, true);
            }
        }

        [Test]
        public void スキャンルート指定で走査範囲を絞れる()
        {
            var root = Path.Combine(Path.GetTempPath(), "novelkit_scan_" + Path.GetRandomFileName());
            try
            {
                Write(root, "Scenarios/main.rb", "narration \"本編の行\"");
                Write(root, "Other/other.rb", "narration \"対象外の行\"");

                var plan = ExtractionPlanner.Scan(root, "Scenarios");

                CollectionAssert.AreEqual(new[] { "本編の行" }, plan.AllCurrentTexts.ToArray());
            }
            finally
            {
                if (Directory.Exists(root)) Directory.Delete(root, true);
            }
        }

        [Test]
        public void 存在しないスキャンルートは例外でなくissueとして報告する()
        {
            var root = Path.Combine(Path.GetTempPath(), "novelkit_scan_" + Path.GetRandomFileName());
            Directory.CreateDirectory(root);
            try
            {
                var plan = ExtractionPlanner.Scan(root, "NotThere");

                CollectionAssert.IsEmpty(plan.CurrentPerFile);
                Assert.AreEqual(1, plan.Issues.Count);
            }
            finally
            {
                if (Directory.Exists(root)) Directory.Delete(root, true);
            }
        }

        private static void Write(string root, string relativePath, string content)
        {
            var full = Path.Combine(root, relativePath.Replace('/', Path.DirectorySeparatorChar));
            Directory.CreateDirectory(Path.GetDirectoryName(full)!);
            File.WriteAllText(full, content);
        }
    }
}
