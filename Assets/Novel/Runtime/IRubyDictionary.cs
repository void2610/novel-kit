#nullable enable

namespace Novel.Runtime
{
    /// <summary>
    /// ルビ辞書本文中の親文字列に対して、辞書定義されたよみを TMP リッチテキストとして付与する。
    ///
    /// 「初出のみ」のような出現回数に依存する表示モードを持つ場合があるため、純粋関数ではなく
    /// インスタンスメソッドにしている。新規開始 / 周回時の状態リセットは <see cref="ResetShown"/> で行う。
    ///
    /// 既定実装は <c>Novel.View.ResourcesRubyDictionary</c> (Resources 配下の rb から読み込む)。
    /// game が独自のロード経路 (JSON / Addressables / hot-reload など) を使う場合は本 interface を実装する。
    /// </summary>
    public interface IRubyDictionary
    {
        /// <summary>本文にルビを付与した TMP リッチテキストを返す (辞書が空ならそのまま)。</summary>
        string ApplyTo(string text);

        /// <summary>「初出のみ」表示の状態をリセットする (新規開始 / 周回の頭で呼ぶ)。</summary>
        void ResetShown();
    }

    /// <summary>
    /// <see cref="IRubyDictionary"/> の no-op 既定 (未配線時のフォールバック)。本文をそのまま返す。
    /// </summary>
    public sealed class NullRubyDictionary : IRubyDictionary
    {
        public string ApplyTo(string text) => text;
        public void ResetShown() { }
    }
}
