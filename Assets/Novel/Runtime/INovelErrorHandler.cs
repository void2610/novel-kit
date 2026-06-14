#nullable enable
using System;

namespace Novel.Runtime
{
    // MRuby 実行時例外の詳細。作家がエラー位置に即気づけるよう、メッセージと（取得できれば）Ruby backtrace を運ぶ
    public readonly struct NovelErrorInfo
    {
        public string ScenarioKey { get; }
        public string Message { get; }      // 例外メッセージ（Ruby のエラー文）
        public string Detail { get; }       // Ruby backtrace 等の詳細（無ければ C# 例外文字列）
        public Exception Exception { get; }

        public NovelErrorInfo(string scenarioKey, string message, string detail, Exception exception)
        {
            ScenarioKey = scenarioKey;
            Message = message;
            Detail = detail;
            Exception = exception;
        }

        public override string ToString() => $"[Novel] シナリオ '{ScenarioKey}' でエラー: {Message}\n{Detail}";
    }

    // MRuby 実行時例外の委譲先。リリースでは NovelResult.Faulted を返しつつここへ通知する。
    // 既定はビルド種別に応じた可視化実装（無音にしない）。
    public interface INovelErrorHandler
    {
        void OnScenarioFaulted(NovelErrorInfo error);
    }
}
