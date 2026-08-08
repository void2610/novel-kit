#nullable enable
namespace Novel.Runtime
{
    // SayCommand から Runtime が構築する提示単位（既読/表示名解決/タグ反映済み）
    public readonly struct NovelLine
    {
        // struct は default や配列要素でコンストラクタを通らず生まれるため、非 null 契約は取得側で担保する
        private readonly string? _text;
        private readonly string? _plainText;

        public string? SpeakerId { get; }
        public string? DisplayName { get; }   // 解決済み表示名。null = ナレーション
        public string Text => _text ?? "";    // ITextResolver 適用後 + 辞書ルビ適用後の表示テキスト（インラインタグ含む）
        // タグと辞書ルビを除いた表示言語 (resolve 後) の平文。View 側の平文検査に使う。
        // 既読 ID はこれではなく resolve 前の原文から runtime が算出する (ロケール不変・localization ADR)。
        // Text から再計算すると辞書ルビのよみが親文字と連なって残る (ルビは lexer タグではなく TMP markup で重なる) ため、
        // ルビ適用前に算出したものを runtime が渡す
        public string PlainText => _plainText ?? "";
        public bool IsAlreadyRead { get; }

        public NovelLine(string? speakerId, string? displayName, string text, string plainText, bool isAlreadyRead)
        {
            SpeakerId = speakerId;
            DisplayName = displayName;
            _text = text;
            _plainText = plainText;
            IsAlreadyRead = isAlreadyRead;
        }
    }
}
