#nullable enable
namespace Novel.Runtime
{
    public enum NovelTokenKind
    {
        Text,        // 表示する素テキスト（Value 未使用、Payload に文字列）
        TmpTag,      // TMP リッチテキストタグ素通し（Payload にタグ全体 例: "<color=#f88>"）
        Wait,        // <w=N> N 秒待機（Value=秒）
        ClickWait,   // <p> クリック待ち
        SpeedPush,   // <speed=Nx> 区間速度変更（Value=倍率）
        SpeedPop,    // </speed>
        Fast,        // <fast> 以降即時表示
        ShakePush,   // <shake>
        ShakePop,    // </shake>
        WavePush,    // <wave>
        WavePop,     // </wave>
        RubyPush,    // <ruby=よみ> Payload=よみ（ふりがな）。直後の Text が親文字、</ruby> で閉じる
        RubyPop,     // </ruby>
        Ignored,     // 認識するが出力しないタグ（任意モジュール対象）
    }

    // タイプライタが逐次 Reveal するトークン。タグは可視文字数に数えない（TMP 技法）
    public readonly struct NovelToken
    {
        public NovelTokenKind Kind { get; }
        public string Payload { get; }   // Text: 素テキスト / TmpTag: タグ文字列 / RubyPush: よみ
        public float Value { get; }      // Wait: 秒 / SpeedPush: 倍率

        public NovelToken(NovelTokenKind kind, string payload = "", float value = 0f)
        {
            Kind = kind;
            Payload = payload;
            Value = value;
        }
    }
}
