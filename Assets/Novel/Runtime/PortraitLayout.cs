#nullable enable
using System;

namespace Novel.Runtime
{
    // レイアウト識別子。 game が ScriptableObject 等で具体的な anchor 表を提供し、 ここでは識別だけ持つ。
    // 既定の命名規約: "single" / "pair" / "trio" / "quad" / "penta" (1〜5 人)。 game 固有の "meeting" 等も可。
    // 構造体にして string Id を持つだけにしているのは、 値の比較 / 辞書キー利用を軽くしつつ enum の閉鎖性を避けるため。
    // IEquatable と Equals/GetHashCode を明示実装することで Dictionary キー利用時の reflection-based 既定比較を避ける。
    public readonly struct PortraitLayout : IEquatable<PortraitLayout>
    {
        public string Id { get; }

        // null を渡された場合は空文字に正規化する (利用側で IsEmpty で判定できる)。
        // ctor 引数を string? にすることで「null も受けるが内部では空文字に揃える」意図を型で表現する。
        public PortraitLayout(string? id) => Id = id ?? "";

        public static PortraitLayout Single => new("single");
        public static PortraitLayout Pair => new("pair");
        public static PortraitLayout Trio => new("trio");
        public static PortraitLayout Quad => new("quad");
        public static PortraitLayout Penta => new("penta");

        public bool IsEmpty => string.IsNullOrEmpty(Id);

        public override string ToString() => Id;

        public bool Equals(PortraitLayout other) => string.Equals(Id, other.Id, StringComparison.Ordinal);
        public override bool Equals(object? obj) => obj is PortraitLayout other && Equals(other);
        public override int GetHashCode() => Id?.GetHashCode() ?? 0;
        public static bool operator ==(PortraitLayout left, PortraitLayout right) => left.Equals(right);
        public static bool operator !=(PortraitLayout left, PortraitLayout right) => !left.Equals(right);
    }
}
