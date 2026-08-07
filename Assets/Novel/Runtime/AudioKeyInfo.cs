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

        /// <summary>
        /// このキーが解決する実アセット (任意・エディタ試聴用)。AudioClip 等の UnityEngine.Object を渡すと、
        /// プロジェクトリファレンスがキー体系に依存せずアセットを特定して試聴できる (エディタ側で GUID 永続化)。
        /// 列挙が軽量であることという契約は変わらない — チャンネルが既に持っている参照を渡すだけにし、
        /// このためのアセットロードはしないこと。
        /// Novel.Runtime の signature にアセット型を持ち込まない方針のため <see cref="object"/> 型
        /// (エディタ側が UnityEngine.Object として解釈する)。
        /// </summary>
        public object? Asset { get; }

        public AudioKeyInfo(string key, AudioKeyKind kind, string? note = null, object? asset = null)
        {
            Key = key;
            Kind = kind;
            Note = note;
            Asset = asset;
        }
    }
}
