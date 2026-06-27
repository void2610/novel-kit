#nullable enable
using System.Collections.Generic;
using Novel.Runtime;
using UnityEngine;

namespace Novel.View
{
    /// <summary>
    /// <see cref="IRubyDictionary"/> の Resources ベース既定実装。
    ///
    /// 定義ファイルは <see cref="DefaultResourcePath"/> (Resources 配下、拡張子なし) に置き、
    /// <c>ruby '漢字', 'かんじ'</c> 形式で記述する。第 3 引数で <c>:first</c> / <c>:once</c> を指定すれば
    /// 「初出のみ」表示になる。エントリは親文字長の降順に保持し、長い語を優先してマッチさせる。
    ///
    /// game 側で独自パスを使いたい場合はコンストラクタ引数で渡すか、<see cref="Load"/> で文字列を直接流し込む。
    /// </summary>
    public sealed class ResourcesRubyDictionary : IRubyDictionary
    {
        /// <summary>ルビ定義ファイルの Resources 既定パス (拡張子なし)。</summary>
        public const string DefaultResourcePath = "Novel/ruby";

        public IReadOnlyList<RubyEntry> Entries => _entries;

        private readonly List<RubyEntry> _entries = new();

        // 「初出のみ」の親文字列で既に一度ルビ表示したもの (周回開始時に ResetShown でクリア)
        private readonly HashSet<string> _shownFirstOnly = new();

        public ResourcesRubyDictionary() : this(DefaultResourcePath) { }

        public ResourcesRubyDictionary(string resourcePath)
        {
            var asset = Resources.Load<TextAsset>(resourcePath);
            if (asset != null) Load(asset.text);
        }

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
