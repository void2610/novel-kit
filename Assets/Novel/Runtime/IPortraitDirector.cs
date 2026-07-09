#nullable enable
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;

namespace Novel.Runtime
{
    // 場面 (stage) の cast (キャラ → slot index) を保持し、 シナリオ書き手から「立ち絵を出す」コマンドを受けて
    // 該当 slot に位置解決する司令塔。 <see cref="IPortraitView"/> の上位で、 シナリオ書き手は slot index を意識しない。
    //
    // 解決順 (Show 時):
    //   1. 直近の Stage 宣言で cast に含まれる → そのスロット index
    //   2. それ以外 → slot 0 にフォールバック + 警告ログ (typo / 宣言漏れ検出のため)
    //
    // ICharacterCatalog の Side は撤去済 (v2)。 デフォルト位置の概念は層を簡素化するため、 stage 宣言で明示する方針。
    public interface IPortraitDirector
    {
        // 場面の cast を宣言する (配列順で index 0..N-1 に割り当て)。 layout 切替も同時に行う。
        UniTask StageAsync(PortraitLayout layout, IReadOnlyList<string> cast, CancellationToken ct);

        // 場面の cast を宣言する (明示 index 指定)。 layout 切替も同時に行う。
        UniTask StageAsync(PortraitLayout layout, IReadOnlyDictionary<string, int> cast, CancellationToken ct);

        // 指定キャラが現在の stage cast に含まれるか。 立ち絵の自動表示可否など、 cast 前提の判定に使う。
        bool IsStaged(string character);

        // 指定キャラを所定の slot に表示する。 cast にいなければ slot 0 にフォールバックして警告。
        UniTask ShowAsync(string character, string portraitKey, CancellationToken ct);

        // 指定キャラを退場させる (cast から外し、 該当 slot を非表示に)。
        UniTask ExitAsync(string character, CancellationToken ct);

        // すべての cast をクリアして場面をリセット (シーン切替時など)。
        UniTask ClearStageAsync(CancellationToken ct);
    }
}
