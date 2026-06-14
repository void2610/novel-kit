#nullable enable
using Novel.Runtime;
using UnityEngine;

namespace Novel.View
{
    // INovelErrorHandler の既定実装。シナリオ名 + Ruby backtrace を Debug.LogError で surface する（無音にしない）。
    // 完全に黙らせたい game は NullErrorHandler を、独自オーバーレイ表示は自前実装を登録すればよい。
    public sealed class DebugNovelErrorHandler : INovelErrorHandler
    {
        public void OnScenarioFaulted(NovelErrorInfo error) => Debug.LogError(error.ToString());
    }
}
