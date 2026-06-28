#nullable enable
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using Novel.Runtime;

namespace Novel.View
{
    // <ruby=よみ>親</ruby> を TMP リッチテキストの座標トリックで「よみを親文字の上に重ねる」文字列へ展開する。
    // 親文字より先によみを描き、退避量だけ x を戻して親文字を描く（よみ・親とも文字なので Reveal の可視数に算入される）。
    public static class RubyMarkup
    {
        private const float RubyScale = 0.5f;        // よみの相対サイズ（親文字に対する割合）
        private const string RubyPercent = "50";     // size=% 表記
        private const string RubyRaiseEm = "0.9";    // 持ち上げ量（em）

        // ルビ定義行: ruby '親', 'よみ'[, :first]  (クォート種別・モード省略に対応)
        private static readonly Regex DeclPattern =
            new(@"ruby\s+(['""])(.+?)\1\s*,\s*(['""])(.+?)\3(?:\s*,\s*[:'""]?([A-Za-z]+)['""]?)?", RegexOptions.Compiled);

        // CJK は概ね 1em/字として中央寄せのオフセットを近似する（長いよみは左寄せに丸める）。よみ・親は noparse で包む。
        public static string BuildOverlay(string baseText, string reading)
        {
            if (string.IsNullOrEmpty(reading)) return "<noparse>" + baseText + "</noparse>";

            var ci = CultureInfo.InvariantCulture;
            float baseWidth = baseText.Length;            // 親文字幅（em 近似）
            float rubyWidth = reading.Length * RubyScale; // よみ幅（em 近似）
            var offset = (baseWidth - rubyWidth) / 2f;
            if (offset < 0f) offset = 0f;
            var back = offset + rubyWidth;

            return $"<space={offset.ToString("0.###", ci)}em><voffset={RubyRaiseEm}em><size={RubyPercent}%><noparse>{reading}</noparse></size></voffset><space=-{back.ToString("0.###", ci)}em><noparse>{baseText}</noparse>";
        }

        /// <summary>
        /// ルビ定義テキスト (rb) を解析する (行頭 <c>#</c> のコメント行・空行は無視)。
        /// 書式: <c>ruby '親', 'よみ'</c> (第 3 引数で <c>:first</c> / <c>:once</c> を指定すると初出のみ表示)。
        /// </summary>
        public static IEnumerable<RubyEntry> Parse(string? rbText)
        {
            var list = new List<RubyEntry>();
            if (string.IsNullOrEmpty(rbText)) return list;

            foreach (var rawLine in rbText!.Split('\n'))
            {
                var line = rawLine.Trim();
                if (line.Length == 0 || line[0] == '#') continue;
                var m = DeclPattern.Match(line);
                if (!m.Success) continue;
                var mode = m.Groups[5].Value;
                var firstOnly = mode.Equals("first", StringComparison.OrdinalIgnoreCase)
                    || mode.Equals("once", StringComparison.OrdinalIgnoreCase);
                list.Add(new RubyEntry(m.Groups[2].Value, m.Groups[4].Value, firstOnly));
            }
            return list;
        }

        /// <summary>本文中の親文字列をルビ付き TMP リッチテキストへ展開する (全出現に付与)。</summary>
        public static string ToRichText(string? text, IReadOnlyList<RubyEntry> entries) => ToRichText(text, entries, null);

        /// <summary>
        /// 本文中の親文字列をルビ付き TMP リッチテキストへ展開する。
        /// <paramref name="shouldRender"/> が与えられた場合、各出現でこれが false を返した語はルビを付けず
        /// 親文字だけを出力する (初出のみ表示などの状態制御に使う)。
        /// <paramref name="entries"/> は親文字長の降順に渡すこと (短い語が長い語の一部を先取りしないように)。
        /// </summary>
        public static string ToRichText(string? text, IReadOnlyList<RubyEntry> entries, Func<RubyEntry, bool>? shouldRender)
        {
            if (string.IsNullOrEmpty(text) || entries == null || entries.Count == 0) return text ?? "";

            var sb = new StringBuilder(text!.Length + 32);
            var i = 0;
            while (i < text.Length)
            {
                // リッチテキストタグはそのまま通す (タグ内部はルビ対象外)
                if (text[i] == '<')
                {
                    var close = text.IndexOf('>', i);
                    if (close == -1)
                    {
                        sb.Append(text, i, text.Length - i);
                        break;
                    }
                    sb.Append(text, i, close - i + 1);
                    i = close + 1;
                    continue;
                }

                var matched = false;
                foreach (var e in entries)
                {
                    var len = e.Base.Length;
                    if (len == 0 || i + len > text.Length) continue;
                    if (string.CompareOrdinal(text, i, e.Base, 0, len) != 0) continue;

                    if (shouldRender == null || shouldRender(e)) sb.Append(BuildOverlay(e.Base, e.Reading));
                    else sb.Append(e.Base);
                    i += len;
                    matched = true;
                    break;
                }
                if (matched) continue;

                sb.Append(text[i]);
                i++;
            }
            return sb.ToString();
        }
    }
}
