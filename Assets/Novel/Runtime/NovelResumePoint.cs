#nullable enable
using System;

namespace Novel.Runtime
{
    // 途中復帰の目標 say 番号。実行位置(Fiber)は直列化できないため、復元済みフラグ下の決定的リプレイ(早送り)で到達する
    public readonly struct NovelResumePoint : IEquatable<NovelResumePoint>
    {
        // 通常表示を再開する say の通し番号（1 始まり）。0 は「復帰なし＝先頭から通常再生」
        public readonly int SayNumber;

        public NovelResumePoint(int sayNumber)
        {
            if (sayNumber < 0) throw new ArgumentOutOfRangeException(nameof(sayNumber), sayNumber, "say number must be >= 0");
            SayNumber = sayNumber;
        }

        // 復帰なし（先頭から通常再生）。PlayAsync(key, ct) と等価
        public static NovelResumePoint None => default;

        // シナリオ末尾まで全て早送りする（マルチセグメント再生で過去セグメントの状態だけ再構築する用途）
        public static NovelResumePoint End => new(int.MaxValue);

        public bool IsNone => SayNumber == 0;

        public bool Equals(NovelResumePoint other) => SayNumber == other.SayNumber;
        public override bool Equals(object? obj) => obj is NovelResumePoint other && Equals(other);
        public override int GetHashCode() => SayNumber;
        public override string ToString() => IsNone ? "None" : SayNumber == int.MaxValue ? "End" : $"Say#{SayNumber}";
    }
}
