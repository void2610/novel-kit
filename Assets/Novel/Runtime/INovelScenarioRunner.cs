using System.Threading;
using Cysharp.Threading.Tasks;

namespace Novel.Runtime
{
    // game が触れる唯一のエントリポイント。1 シナリオを完了まで再生し結果を返す
    public interface INovelScenarioRunner
    {
        // 実行中の例外は NovelResult.Faulted/Cancelled に畳む（フェイルセーフ）
        // 再生中の再呼び出しは switch-latest: 前再生を cancel し後始末を待って差し替える（前呼び出しは Cancelled）
        UniTask<NovelResult> PlayAsync(string scenarioKey, CancellationToken ct);
    }
}
