#nullable enable
namespace Novel.Runtime
{
    /// <summary>音キーの種別。</summary>
    public enum AudioKeyKind
    {
        Bgm,
        Se,
    }

    /// <summary>
    /// <see cref="IAudioChannel"/> が解決できる音キーの目録エントリ (project-reference ADR)。
    /// エディタのプロジェクトリファレンスや将来のシナリオ検証が「使えるキーの一覧」として読む。
    /// </summary>
    public readonly struct AudioKeyInfo
    {
        public string Key { get; }
        public AudioKeyKind Kind { get; }

        /// <summary>ライター向けメモ (任意)。</summary>
        public string? Note { get; }

        public AudioKeyInfo(string key, AudioKeyKind kind, string? note = null)
        {
            Key = key;
            Kind = kind;
            Note = note;
        }
    }
}
