#nullable enable

namespace Novel.Runtime
{
    /// <summary>ルビ 1 件 (親文字列・よみ・表示モード)。</summary>
    public readonly struct RubyEntry
    {
        public readonly string Base;
        public readonly string Reading;

        /// <summary>最初の 1 回だけ表示するか (true=初出のみ / false=常に表示)。</summary>
        public readonly bool FirstOnly;

        public RubyEntry(string baseText, string reading, bool firstOnly = false)
        {
            Base = baseText;
            Reading = reading;
            FirstOnly = firstOnly;
        }
    }
}
