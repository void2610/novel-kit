#nullable enable
using System;
using System.Reflection;

namespace Novel.Runtime
{
    // 例外から作家向けエラー情報を組み立てる。MRubyRaiseException があれば Ruby backtrace を surface する
    // （GetBacktraceString のシグネチャはバージョン差があるためリフレクションで安全に呼ぶ）。
    internal static class NovelErrorReport
    {
        public static NovelErrorInfo Describe(string scenarioKey, Exception ex)
        {
            var backtrace = TryGetRubyBacktrace(ex);
            var detail = string.IsNullOrEmpty(backtrace) ? ex.ToString() : backtrace!;
            return new NovelErrorInfo(scenarioKey, ex.Message, detail, ex);
        }

        private static string? TryGetRubyBacktrace(Exception ex)
        {
            var method = ex.GetType().GetMethod("GetBacktraceString", Type.EmptyTypes);
            if (method == null || method.ReturnType != typeof(string)) return null;
            try { return method.Invoke(ex, null) as string; }
            catch { return null; }
        }
    }
}
