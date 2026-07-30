#nullable enable
namespace Novel.Runtime
{
    // SayCommand から Runtime が構築する提示単位（既読/表示名解決/タグ反映済み）
    public readonly struct NovelLine
    {
        public string? SpeakerId { get; }
        public string? DisplayName { get; }   // 解決済み表示名。null = ナレーション
        public string Text { get; }           // ITextResolver 適用後 + 辞書ルビ適用後の表示テキスト（インラインタグ含む）
        // タグと辞書ルビを除いた平文。既読 ID の算出基準そのもので、View 側の平文検査もこれを使う。
        // Text から再計算すると辞書ルビのよみが親文字と連なって残る (ルビは lexer タグではなく TMP markup で重なる) ため、
        // ルビ適用前に算出したものを runtime が渡す
        public string PlainText { get; }
        public bool IsAlreadyRead { get; }

        public NovelLine(string? speakerId, string? displayName, string text, string plainText, bool isAlreadyRead)
        {
            SpeakerId = speakerId;
            DisplayName = displayName;
            Text = text;
            PlainText = plainText;
            IsAlreadyRead = isAlreadyRead;
        }
    }
}
