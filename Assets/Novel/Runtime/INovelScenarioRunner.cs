using System.Threading;
using Cysharp.Threading.Tasks;

namespace Novel.Runtime
{
    // game が触れる唯一のエントリポイント。1 シナリオを完了まで再生し結果を返す
    public interface INovelScenarioRunner
    {
        // シナリオ実行中の例外は握って NovelResult.Faulted/Cancelled に畳む（フェイルセーフ）。
        // ただし再生中（前の PlayAsync 完了前）の再入呼び出しだけは InvalidOperationException を投げる
        // — 単一 MRubyState を共有するための fail-fast で、これは API 誤用（配線バグ）の検出であり、
        //   シナリオ/コンテンツ障害を表す Faulted とは別カテゴリ。呼び出し側は再生を直列化すること。
        UniTask<NovelResult> PlayAsync(string scenarioKey, CancellationToken ct);
    }
}
