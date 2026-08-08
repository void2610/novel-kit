#nullable enable
using System;
using System.Collections.Generic;
using Novel.Runtime;

namespace Novel.Editor.Localization
{
    public enum TextChangeKind
    {
        TagOnly,      // タグ除去後の平文が一致（タグだけの変更）→ 訳保持 + タグ移植フラグ
        FuzzyCarry,   // 高類似（誤字修正・軽微な推敲）→ 訳保持 + fuzzy（要再確認）
        Rewritten,    // 低類似（リライト）→ 旧訳を参考退避して未訳化（stale 訳を出さない）
    }

    public readonly struct TextChange
    {
        public readonly int PreviousIndex;
        public readonly int CurrentIndex;
        public readonly TextChangeKind Kind;
        public readonly float Similarity;   // タグ除去後平文の正規化類似度 [0,1]
        public TextChange(int previousIndex, int currentIndex, TextChangeKind kind, float similarity)
        {
            PreviousIndex = previousIndex;
            CurrentIndex = currentIndex;
            Kind = kind;
            Similarity = similarity;
        }
    }

    public sealed class FileTextDiff
    {
        public readonly List<(int Previous, int Current)> Unchanged = new();
        public readonly List<TextChange> Changes = new();
        public readonly List<int> AddedCurrent = new();     // 対にならなかった新出（未訳の新規）
        public readonly List<int> RemovedPrevious = new();  // 対にならなかった消滅（deprecated 候補）
    }

    /// <summary>
    /// 追跡エンジンの中核: 前回抽出時と今回の原文列（同一ファイル・出現順）を突き合わせ、
    /// 「どの行がどの行の書き直しか」を位置 + 類似度で対応付ける（localization-unity-package ADR）。
    /// 一致行を LCS でアンカーにし、アンカー間のギャップ内だけで変化行を対にするため、
    /// ファイル全体の挿入・削除で位置がずれても誤対応しにくい。
    /// </summary>
    public static class TrackedTextDiffer
    {
        // 分類しきい値。平文一致 → TagOnly / これ以上 → FuzzyCarry / PairThreshold 以上 → Rewritten /
        // 未満はそもそも同一行と見なさない（独立した削除 + 追加）
        public const float FuzzyThreshold = 0.55f;
        public const float PairThreshold = 0.25f;

        public static FileTextDiff Diff(IReadOnlyList<string> previous, IReadOnlyList<string> current)
        {
            var diff = new FileTextDiff();
            var anchors = LongestCommonSubsequence(previous, current);
            diff.Unchanged.AddRange(anchors);

            // アンカー間のギャップごとに、消えた行と現れた行を類似度の高い順に貪欲に対にする
            var prevGapStart = 0;
            var currGapStart = 0;
            foreach (var (prevAnchor, currAnchor) in AnchorsWithSentinel(anchors, previous.Count, current.Count))
            {
                PairGap(diff, previous, current, prevGapStart, prevAnchor, currGapStart, currAnchor);
                prevGapStart = prevAnchor + 1;
                currGapStart = currAnchor + 1;
            }
            return diff;
        }

        private static IEnumerable<(int Prev, int Curr)> AnchorsWithSentinel(
            List<(int Previous, int Current)> anchors, int prevCount, int currCount)
        {
            foreach (var a in anchors) yield return a;
            yield return (prevCount, currCount);   // 末尾ギャップ用の番兵
        }

        private static void PairGap(FileTextDiff diff, IReadOnlyList<string> previous, IReadOnlyList<string> current,
            int prevStart, int prevEnd, int currStart, int currEnd)
        {
            var removed = new List<int>();
            for (var i = prevStart; i < prevEnd; i++) removed.Add(i);
            var added = new List<int>();
            for (var j = currStart; j < currEnd; j++) added.Add(j);
            if (removed.Count == 0 && added.Count == 0) return;

            // 全組み合わせの類似度を出し、高い順に採る（ギャップは通常ごく小さい）
            var candidates = new List<(float Sim, int Prev, int Curr)>();
            foreach (var p in removed)
            {
                var prevPlain = NovelTagLexer.ToPlainText(previous[p]);
                foreach (var c in added)
                {
                    var sim = Similarity(prevPlain, NovelTagLexer.ToPlainText(current[c]));
                    if (sim >= PairThreshold) candidates.Add((sim, p, c));
                }
            }
            candidates.Sort((a, b) => b.Sim.CompareTo(a.Sim));

            var usedPrev = new HashSet<int>();
            var usedCurr = new HashSet<int>();
            foreach (var (sim, p, c) in candidates)
            {
                if (usedPrev.Contains(p) || usedCurr.Contains(c)) continue;
                usedPrev.Add(p);
                usedCurr.Add(c);
                var kind = NovelTagLexer.ToPlainText(previous[p]) == NovelTagLexer.ToPlainText(current[c])
                    ? TextChangeKind.TagOnly
                    : sim >= FuzzyThreshold ? TextChangeKind.FuzzyCarry : TextChangeKind.Rewritten;
                diff.Changes.Add(new TextChange(p, c, kind, sim));
            }
            foreach (var p in removed) if (!usedPrev.Contains(p)) diff.RemovedPrevious.Add(p);
            foreach (var c in added) if (!usedCurr.Contains(c)) diff.AddedCurrent.Add(c);
        }

        // 完全一致行の LCS（DP）。戻り値は (prevIndex, currIndex) の昇順ペア
        private static List<(int Previous, int Current)> LongestCommonSubsequence(
            IReadOnlyList<string> previous, IReadOnlyList<string> current)
        {
            var n = previous.Count;
            var m = current.Count;
            var dp = new int[n + 1, m + 1];
            for (var i = n - 1; i >= 0; i--)
                for (var j = m - 1; j >= 0; j--)
                    dp[i, j] = previous[i] == current[j]
                        ? dp[i + 1, j + 1] + 1
                        : Math.Max(dp[i + 1, j], dp[i, j + 1]);

            var result = new List<(int, int)>();
            var a = 0;
            var b = 0;
            while (a < n && b < m)
            {
                if (previous[a] == current[b])
                {
                    result.Add((a, b));
                    a++;
                    b++;
                }
                else if (dp[a + 1, b] >= dp[a, b + 1]) a++;
                else b++;
            }
            return result;
        }

        // 正規化 Levenshtein 類似度 [0,1]。1 = 同一
        public static float Similarity(string a, string b)
        {
            if (a.Length == 0 && b.Length == 0) return 1f;
            var max = Math.Max(a.Length, b.Length);
            return 1f - (float)Levenshtein(a, b) / max;
        }

        private static int Levenshtein(string a, string b)
        {
            var prev = new int[b.Length + 1];
            var curr = new int[b.Length + 1];
            for (var j = 0; j <= b.Length; j++) prev[j] = j;
            for (var i = 1; i <= a.Length; i++)
            {
                curr[0] = i;
                for (var j = 1; j <= b.Length; j++)
                {
                    var cost = a[i - 1] == b[j - 1] ? 0 : 1;
                    curr[j] = Math.Min(Math.Min(curr[j - 1] + 1, prev[j] + 1), prev[j - 1] + cost);
                }
                (prev, curr) = (curr, prev);
            }
            return prev[b.Length];
        }
    }
}
