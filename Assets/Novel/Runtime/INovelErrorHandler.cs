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

        /// <summary>
        /// 落ちる直前に処理した say の通し番号（1 始まり。0 = セリフ表示前）。
        /// .mrb にデバッグ情報が無く Ruby の行番号を得られないため、これが位置の手掛かりになる。
        /// </summary>
        public int SayNumber { get; }

        public NovelErrorInfo(string scenarioKey, string message, string detail, Exception exception, int sayNumber = 0)
        {
            ScenarioKey = scenarioKey;
            Message = message;
            Detail = detail;
            Exception = exception;
            SayNumber = sayNumber;
        }

        public override string ToString()
        {
            var where = SayNumber > 0
                ? $"{SayNumber} 番目のセリフまで進んだ時点"
                : "セリフを 1 つも表示しないうちに";
            return $"[Novel] シナリオ '{ScenarioKey}' の {where}でエラー: {Message}\n{Detail}";
        }
    }

    // MRuby 実行時例外の委譲先。リリースでは NovelResult.Faulted を返しつつここへ通知する。
    // 既定はビルド種別に応じた可視化実装（無音にしない）。
    public interface INovelErrorHandler
    {
        void OnScenarioFaulted(NovelErrorInfo error);

        /// <summary>
        /// 例外にならない不具合（キー解決の失敗など）の通知。独自オーバーレイに出したい game だけ実装すればよく、
        /// 未実装でもライブラリが dev ビルドでログを出すため無言にはならない（既存実装を壊さないための default）。
        /// </summary>
        void OnRuntimeIssue(NovelIssueInfo issue) { }
    }
}
