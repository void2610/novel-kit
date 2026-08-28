#nullable enable

namespace Novel.Runtime
{
    /// <summary>再生を止めるほどではないが、作家の意図どおりに動いていない事象の種別。</summary>
    public enum NovelIssueKind
    {
        /// <summary>シナリオキーに対応するバイトコードが取れなかった (キーの誤記・アセット未配置)。</summary>
        ScenarioNotFound,

        /// <summary>糖衣定義 preamble が取れなかった (これ以降、糖衣コマンドが未定義になる)。</summary>
        PreambleNotFound,

        /// <summary>画像キーからスプライトを解決できなかった (キーの誤記・root 違い・アセット未配置)。</summary>
        SpriteNotFound,
    }

    /// <summary>
    /// 例外にはならないが黙って無視すると原因の掴めない事象。<see cref="NovelErrorInfo"/> が
    /// 「シナリオが落ちた」を運ぶのに対し、こちらは「動いてはいるが指定が効いていない」を運ぶ。
    /// </summary>
    public readonly struct NovelIssueInfo
    {
        public NovelIssueKind Kind { get; }

        /// <summary>再生中のシナリオキー (再生開始前なら空)。</summary>
        public string ScenarioKey { get; }

        /// <summary>解決できなかったキーなど、事象の対象 (無ければ空)。</summary>
        public string Subject { get; }

        /// <summary>事象の説明。場所は <see cref="ScenarioKey"/> が持つため、ここでは繰り返さない。</summary>
        public string Message { get; }

        public NovelIssueInfo(NovelIssueKind kind, string scenarioKey, string subject, string message)
        {
            Kind = kind;
            ScenarioKey = scenarioKey;
            Subject = subject;
            Message = message;
        }

        public override string ToString()
        {
            var where = string.IsNullOrEmpty(ScenarioKey) ? "" : $"シナリオ '{ScenarioKey}': ";
            return $"[Novel] {where}{Message}";
        }
    }
}
