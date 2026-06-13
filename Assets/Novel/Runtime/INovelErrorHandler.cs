#nullable enable
using System;

namespace Novel.Runtime
{
    // MRuby 実行時例外の委譲先。リリースでは NovelResult.Faulted を返しつつここへ通知する
    public interface INovelErrorHandler
    {
        void OnScenarioFaulted(string scenarioKey, Exception exception);
    }
}
