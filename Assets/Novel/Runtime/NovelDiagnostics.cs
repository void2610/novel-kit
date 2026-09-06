#nullable enable
using UnityEngine;

namespace Novel.Runtime
{
    /// <summary>
    /// 「動いてはいるが指定が効いていない」事象の報告口 (error-handling ADR)。
    /// dev ビルドでのログと <see cref="INovelErrorHandler"/> への通知をここ 1 か所に集約し、
    /// 検知点が増えても報告の作法がぶれないようにする。独自コマンドモジュール (opt-in アセンブリ) からも使う。
    /// </summary>
    public static class NovelDiagnostics
    {
        public static void Report(INovelErrorHandler? handler, NovelIssueInfo issue)
        {
            // 本番では黙り (game が拾いたければハンドラ経由で受ける)、dev では作家のミスを見落とされない赤で顕在化する
            if (Debug.isDebugBuild) Debug.LogError(issue.ToString());
            handler?.OnRuntimeIssue(issue);
        }

        public static void ScenarioNotFound(INovelErrorHandler? handler, string scenarioKey) =>
            Report(handler, new NovelIssueInfo(NovelIssueKind.ScenarioNotFound, scenarioKey, scenarioKey,
                "バイトコードを取得できなかったため、何も再生せずに終了しました。" +
                "シナリオキーの誤記か、.rb アセットの未配置が考えられます。"));

        public static void PreambleNotFound(INovelErrorHandler? handler, string sourceType) =>
            Report(handler, new NovelIssueInfo(NovelIssueKind.PreambleNotFound, "", sourceType,
                $"{sourceType} が preamble を返さなかったため、そこで定義される糖衣コマンドは未定義のままです。"));

        public static void SpriteNotFound(INovelErrorHandler? handler, string scenarioKey, string key) =>
            Report(handler, new NovelIssueInfo(NovelIssueKind.SpriteNotFound, scenarioKey, key,
                $"画像キー '{key}' を解決できなかったため、その表示は空になります。" +
                "キーの誤記か、ローダの root 違い、アセットの未配置が考えられます " +
                "(使えるキーは Novel > Project Reference で確認できます)。"));

        public static void EffectNotFound(INovelErrorHandler? handler, string scenarioKey, string key, string hint) =>
            Report(handler, new NovelIssueInfo(NovelIssueKind.EffectNotFound, scenarioKey, key,
                $"演出キー '{key}' に対応する定義が見つからなかったため、何も再生しません。{hint}"));
    }
}
