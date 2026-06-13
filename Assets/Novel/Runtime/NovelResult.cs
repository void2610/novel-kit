namespace Novel.Runtime
{
    // PlayAsync の完了状態。分岐に必要な outcome は IStateStore に残る
    public enum NovelResult
    {
        Completed,   // 正常完了
        Cancelled,   // CancellationToken によるキャンセル
        Faulted,     // MRuby 実行時例外でフェイルセーフ終了（INovelErrorHandler に委譲）
    }
}
