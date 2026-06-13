#nullable enable
namespace Novel.Runtime
{
    // say テキストの解決フック。多言語化はこの実装差し替えで非破壊に後付けする
    public interface ITextResolver
    {
        string Resolve(string raw);
    }

    // 既定の恒等変換（日本語直書きをそのまま使う）
    public sealed class IdentityTextResolver : ITextResolver
    {
        public string Resolve(string raw) => raw;
    }
}
