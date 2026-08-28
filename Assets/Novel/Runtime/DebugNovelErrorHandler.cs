#nullable enable
using UnityEngine;

namespace Novel.Runtime
{
    // INovelErrorHandler の既定実装。シナリオ名 + Ruby backtrace を Debug.LogError で surface する（無音にしない）。
    // 完全に黙らせたい game は NullErrorHandler を、独自オーバーレイ表示は自前実装を登録すればよい。
    // 不具合通知 (OnRuntimeIssue) は NovelDiagnostics が dev ログを出す側なので、ここでは二重に出さない。
    public sealed class DebugNovelErrorHandler : INovelErrorHandler
    {
        public void OnScenarioFaulted(NovelErrorInfo error) => Debug.LogError(error.ToString());
    }
}
