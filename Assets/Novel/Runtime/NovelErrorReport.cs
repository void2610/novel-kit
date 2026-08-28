#nullable enable
using System;

namespace Novel.Runtime
{
    // 例外から作家向けエラー情報を組み立てる。MRubyRaiseException があれば Ruby backtrace を surface する
    // （GetBacktraceString のシグネチャはバージョン差があるためリフレクションで安全に呼ぶ）。
    internal static class NovelErrorReport
    {
        public static NovelErrorInfo Describe(string scenarioKey, Exception ex, int sayNumber = 0)
        {
            var backtrace = TryGetRubyBacktrace(ex);
            var detail = string.IsNullOrEmpty(backtrace) ? ex.ToString() : backtrace!;
            return new NovelErrorInfo(scenarioKey, ex.Message, detail, ex, sayNumber);
        }

        /// <summary>
        /// Ruby 側の backtrace を取り出す。実体は 例外.ExceptionObject.Backtrace.ToString(state)。
        /// MRubyState.GetBacktraceString() は VM を抜けた後では空を返すため使えない。
        /// バージョン差を吸収するためリフレクションで辿り、取れなければ null (呼び元が C# 例外文字列へ落とす)。
        /// </summary>
        private static string? TryGetRubyBacktrace(Exception ex)
        {
            try
            {
                var type = ex.GetType();
                var state = type.GetProperty("State")?.GetValue(ex);
                var exceptionObject = type.GetProperty("ExceptionObject")?.GetValue(ex);
                if (state == null || exceptionObject == null) return null;

                var backtrace = exceptionObject.GetType().GetProperty("Backtrace")?.GetValue(exceptionObject);
                var toString = backtrace?.GetType().GetMethod("ToString", new[] { state.GetType() });
                if (toString == null || toString.ReturnType != typeof(string)) return null;

                var text = toString.Invoke(backtrace, new[] { state }) as string;
                return string.IsNullOrWhiteSpace(text) ? null : text;
            }
            catch
            {
                return null;
            }
        }
    }
}
