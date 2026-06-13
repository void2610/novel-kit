#nullable enable
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace Novel.Runtime
{
    // 行内インラインタグの字句解析（inline-tags ADR）。記法は TMP <...> 単一・ライブラリ所有 lexer。
    // 制御タグは制御トークンへ抽出、TMP スタイルは素通し、未知の '<' はリテラル文字として扱う。
    // TMP 文字列化/エスケープ・頂点アニメ駆動は View 層の責務。
    public static class NovelTagLexer
    {
        // 素通しする TMP リッチテキストタグ名（小文字・先頭 '/' 除去後で照合）
        private static readonly HashSet<string> TmpStyleTags = new()
        {
            "color", "b", "i", "u", "s", "size", "sub", "sup", "mark", "link",
            "align", "voffset", "space", "indent", "nobr", "sprite", "font",
            "cspace", "line-height", "lowercase", "uppercase", "smallcaps",
            "gradient", "rotate", "width", "style", "pos", "alpha", "noparse",
        };

        public static IReadOnlyList<NovelToken> Parse(string raw)
        {
            var tokens = new List<NovelToken>();
            var text = new StringBuilder();

            void FlushText()
            {
                if (text.Length == 0) return;
                tokens.Add(new NovelToken(NovelTokenKind.Text, text.ToString()));
                text.Clear();
            }

            int i = 0;
            while (i < raw.Length)
            {
                char c = raw[i];
                if (c != '<')
                {
                    text.Append(c);
                    i++;
                    continue;
                }

                int close = raw.IndexOf('>', i + 1);
                if (close < 0)
                {
                    // 閉じない '<' はリテラル
                    text.Append(c);
                    i++;
                    continue;
                }

                var inner = raw.Substring(i + 1, close - i - 1);   // '<' '>' の中身
                if (TryClassify(inner, out var token, out var isTmpPassthrough))
                {
                    FlushText();
                    if (isTmpPassthrough)
                        tokens.Add(new NovelToken(NovelTokenKind.TmpTag, "<" + inner + ">"));
                    else if (token.Kind != NovelTokenKind.Ignored)
                        tokens.Add(token);
                    i = close + 1;
                }
                else
                {
                    // 未知タグ: '<' をリテラル文字として扱い 1 文字進める
                    text.Append(c);
                    i++;
                }
            }

            FlushText();
            return tokens;
        }

        // 全タグ除去後の素テキスト（既読ハッシュ・バックログ用）
        public static string ToPlainText(string raw)
        {
            var sb = new StringBuilder(raw.Length);
            foreach (var t in Parse(raw))
                if (t.Kind == NovelTokenKind.Text)
                    sb.Append(t.Payload);
            return sb.ToString();
        }

        private static bool TryClassify(string inner, out NovelToken token, out bool isTmpPassthrough)
        {
            token = default;
            isTmpPassthrough = false;
            if (inner.Length == 0) return false;

            // タグ名（'=' や属性の前まで、先頭 '/' は閉じタグ）
            var isClose = inner[0] == '/';
            var body = isClose ? inner.Substring(1) : inner;
            var nameEnd = body.IndexOf('=');
            var name = (nameEnd >= 0 ? body.Substring(0, nameEnd) : body).Trim().ToLowerInvariant();

            switch (name)
            {
                case "w":
                    token = new NovelToken(NovelTokenKind.Wait, value: ParseFloat(body, nameEnd));
                    return true;
                case "p":
                    token = new NovelToken(NovelTokenKind.ClickWait);
                    return true;
                case "fast":
                    token = new NovelToken(NovelTokenKind.Fast);
                    return true;
                case "speed":
                    token = new NovelToken(isClose ? NovelTokenKind.SpeedPop : NovelTokenKind.SpeedPush,
                        value: isClose ? 0f : ParseFloat(body, nameEnd));
                    return true;
                case "shake":
                    token = new NovelToken(isClose ? NovelTokenKind.ShakePop : NovelTokenKind.ShakePush);
                    return true;
                case "wave":
                    token = new NovelToken(isClose ? NovelTokenKind.WavePop : NovelTokenKind.WavePush);
                    return true;
                case "ruby":
                    // ruby は任意モジュール（inline-tags ADR）。コアは認識のみで読みは展開せず、漢字本体は素通し
                    token = new NovelToken(NovelTokenKind.Ignored);
                    return true;
            }

            if (TmpStyleTags.Contains(name))
            {
                isTmpPassthrough = true;
                return true;
            }

            return false;
        }

        // "2x" / "0.5" 等の値を float へ（'x' は倍率サフィックスとして除去）
        private static float ParseFloat(string body, int eqIndex)
        {
            if (eqIndex < 0) return 0f;
            var v = body.Substring(eqIndex + 1).Trim().TrimEnd('x', 'X');
            return float.TryParse(v, NumberStyles.Float, CultureInfo.InvariantCulture, out var f) ? f : 0f;
        }
    }
}
