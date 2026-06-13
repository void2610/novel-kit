using System.Threading;
using Cysharp.Threading.Tasks;

namespace Novel.Runtime
{
    // game が触れる唯一のエントリポイント。1 シナリオを完了まで再生し結果を返す
    public interface INovelScenarioRunner
    {
        UniTask<NovelResult> PlayAsync(string scenarioKey, CancellationToken ct);
    }
}
