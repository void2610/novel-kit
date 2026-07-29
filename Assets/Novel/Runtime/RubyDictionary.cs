#nullable enable
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;

namespace Novel.Runtime
{
    /// <summary>
    /// <see cref="IRubyDictionary"/> のロード手段非依存の実装。
    /// 定義テキストは <see cref="Load"/> で直接流し込むか、<see cref="LoadFromAsync"/> で
    /// <see cref="ITextAssetLoader"/> (Resources / Addressables 等) から読み込む。
    /// 書式は <c>ruby '漢字', 'かんじ'</c> (第 3 引数 <c>:first</c> / <c>:once</c> で初出のみ表示)。
    /// エントリは親文字長の降順に保持し、長い語を優先してマッチさせる。
    /// </summary>
    public class RubyDictionary : IRubyDictionary
    {
        /// <summary>ルビ定義ファイルの既定キー (Resources 相対パス / アドレス、拡張子なし)。</summary>
        public const string DefaultKey = "Novel/ruby";

        public IReadOnlyList<RubyEntry> Entries => _entries;

        private readonly List<RubyEntry> _entries = new();

        // 「初出のみ」の親文字列で既に一度ルビ表示したもの (周回開始時に ResetShown でクリア)
        private readonly HashSet<string> _shownFirstOnly = new();

        /// <summary>本文にルビを付与した TMP リッチテキストを返す (辞書が空ならそのまま)。</summary>
        public string ApplyTo(string text) => RubyMarkup.ToRichText(text, _entries, ShouldRender);

        /// <summary>「初出のみ」表示の状態をリセットする (新規開始 / 周回の頭で呼ぶ)。</summary>
        public void ResetShown() => _shownFirstOnly.Clear();

        /// <summary>ルビ定義テキストを読み込む (親文字長の降順に整列して保持)。</summary>
        public void Load(string rbText)
        {
            _entries.Clear();
            _entries.AddRange(RubyMarkup.Parse(rbText));
            // 長い親文字列を優先 (短い語が長い語の一部を先取りしないように)
            _entries.Sort((a, b) => b.Base.Length - a.Base.Length);
            _shownFirstOnly.Clear();
        }

        /// <summary>ローダー経由でルビ定義テキストを読み込む (キーが見つからなければ空辞書のまま)。</summary>
        public async UniTask LoadFromAsync(ITextAssetLoader loader, string key, CancellationToken ct)
        {
            var text = await loader.LoadTextAsync(key, ct);
            if (text != null) Load(text);
        }

        // 出現ごとの付与可否: 常時表示は常に true。 初出のみは未表示の初回だけ true (以降は親文字のみ)
        private bool ShouldRender(RubyEntry entry)
        {
            if (!entry.FirstOnly) return true;
            if (_shownFirstOnly.Contains(entry.Base)) return false;
            _shownFirstOnly.Add(entry.Base);
            return true;
        }
    }
}
