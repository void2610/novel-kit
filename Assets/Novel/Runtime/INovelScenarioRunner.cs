using System.Threading;
using Cysharp.Threading.Tasks;

namespace Novel.Runtime
{
    // game が触れる唯一のエントリポイント。1 シナリオを完了まで再生し結果を返す
    public interface INovelScenarioRunner
    {
        // シナリオ実行中の例外は握って NovelResult.Faulted/Cancelled に畳む（フェイルセーフ）。
        // 再生中（前の PlayAsync 完了前）に再度呼ぶと switch-latest: 進行中の再生を cancel し、
        // その後始末（単一 MRubyState の巻き戻し）完了を待ってから新シナリオへ差し替える。
        // 差し替えられた前呼び出しは NovelResult.Cancelled を受け取る。呼び出し側は直列化を意識しなくてよい。
        UniTask<NovelResult> PlayAsync(string scenarioKey, CancellationToken ct);
    }
}
