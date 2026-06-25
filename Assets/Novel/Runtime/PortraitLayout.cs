#nullable enable
namespace Novel.Runtime
{
    // レイアウト識別子。game が ScriptableObject 等で具体的な anchor 表を提供し、 ここでは識別だけ持つ。
    // 既定の命名規約: "single" / "pair" / "trio" / "quad" / "penta" (1〜5 人)。 game 固有の "meeting" 等も可。
    // 構造体にして string Id を持つだけにしているのは、 値の比較 / 辞書キー利用を軽くしつつ enum の閉鎖性を避けるため。
    public readonly struct PortraitLayout
    {
        public string Id { get; }

        public PortraitLayout(string id) => Id = id ?? "";

        public static PortraitLayout Single => new("single");
        public static PortraitLayout Pair => new("pair");
        public static PortraitLayout Trio => new("trio");
        public static PortraitLayout Quad => new("quad");
        public static PortraitLayout Penta => new("penta");

        public bool IsEmpty => string.IsNullOrEmpty(Id);

        public override string ToString() => Id;
    }
}
