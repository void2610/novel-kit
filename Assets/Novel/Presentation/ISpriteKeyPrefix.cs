#nullable enable

namespace Novel.Assets
{
    /// <summary>
    /// <see cref="ISpriteLoader"/> が「シナリオのキーの前に自分で付けるプレフィックス」を名乗る任意の口
    /// (project-reference ADR)。実装しなくてもロードの挙動は変わらない。
    ///
    /// エディタのプロジェクトリファレンスは Resources 相対パスからキー候補を組み立てるが、
    /// ローダが root を持つ場合その分だけ実際のキーとズレる。これを実装しておくと、
    /// ウィンドウが「シナリオにそのまま書けるキー」を正確に表示できる。
    /// </summary>
    public interface ISpriteKeyPrefix
    {
        /// <summary>キーの前に付与されるプレフィックス (付与しないなら空文字)。</summary>
        string KeyPrefix { get; }
    }
}
