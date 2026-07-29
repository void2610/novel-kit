#nullable enable
using UnityEngine;

namespace Novel.View
{
    /// <summary>
    /// <see cref="RubyDictionary"/> の Resources ベース既定実装。
    /// 定義ファイルは <see cref="DefaultResourcePath"/> (Resources 配下、拡張子なし) に置く。
    /// game 側で独自パスを使いたい場合はコンストラクタ引数で渡すか、<see cref="RubyDictionary.Load"/> で文字列を直接流し込む。
    /// </summary>
    public sealed class ResourcesRubyDictionary : RubyDictionary
    {
        /// <summary>ルビ定義ファイルの Resources 既定パス (拡張子なし)。</summary>
        public const string DefaultResourcePath = "Novel/ruby";

        public ResourcesRubyDictionary() : this(DefaultResourcePath) { }

        public ResourcesRubyDictionary(string resourcePath)
        {
            var asset = Resources.Load<TextAsset>(resourcePath);
            if (asset != null) Load(asset.text);
        }
    }
}
