#nullable enable
namespace Novel.Runtime
{
    // SayCommand から Runtime が構築する提示単位（既読/表示名解決/タグ反映済み）
    public readonly struct NovelLine
    {
        public string? SpeakerId { get; }
        public string? DisplayName { get; }   // 解決済み表示名。null = ナレーション
        public string Text { get; }           // ITextResolver 適用後の表示テキスト（インラインタグ含む生テキスト）
        public bool IsAlreadyRead { get; }

        public NovelLine(string? speakerId, string? displayName, string text, bool isAlreadyRead)
        {
            SpeakerId = speakerId;
            DisplayName = displayName;
            Text = text;
            IsAlreadyRead = isAlreadyRead;
        }
    }
}
