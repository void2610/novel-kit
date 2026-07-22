using System.Threading;
using Cysharp.Threading.Tasks;

namespace Novel.Runtime
{
    // game が触れるエントリポイント。1 シナリオを完了まで再生し、状態スナップショットを出し入れする
    public interface INovelScenarioRunner
    {
        // 実行中の例外は NovelResult.Faulted/Cancelled に畳む（フェイルセーフ）
        // 再生中の再呼び出しは switch-latest: 前再生を cancel し後始末を待って差し替える（前呼び出しは Cancelled）
        UniTask<NovelResult> PlayAsync(string scenarioKey, CancellationToken ct);

        // 途中復帰つき再生。RestoreState 済みフラグ下でのみ分岐リプレイが決定的になる (呼び出し前提)
        UniTask<NovelResult> PlayAsync(string scenarioKey, NovelResumePoint resume, CancellationToken ct);

        // 現在の再生で処理を開始した say の通し番号（1 始まり・0 = 未開始）。セリフ単位セーブのカーソルとして保存する
        int CurrentSayNumber { get; }

        // 永続状態(フラグ/変数 + 既読)のスナップショットを引く。game が保存したいタイミングで呼び、
        // 直列化(NovelSaveData / NovelSaveSerializer)と保存は game 側で行う（進行とセーブは game 所有）。
        NovelStateSnapshot CaptureState();

        // 保存済みスナップショットを復元する。continue 時など、次の PlayAsync より前に呼ぶ
        // （PlayAsync 実行中の呼び出しは想定しない）。
        void RestoreState(NovelStateSnapshot snapshot);
    }
}
