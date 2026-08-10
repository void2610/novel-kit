#nullable enable
using System.Collections.Generic;
using System.Text;

namespace Novel.Editor.Localization
{
    // .rb から抽出したローカライズ対象テキスト 1 件（出現順が追跡の基準になる）
    public readonly struct ScannedText
    {
        public readonly string Text;        // アンエスケープ済み原文（ランタイムが Resolve に渡す文字列と一致させる）
        public readonly int LineNumber;     // 1 始まり（レポート表示用）
        public ScannedText(string text, int lineNumber)
        {
            Text = text;
            LineNumber = lineNumber;
        }
    }

    // 抽出できなかった箇所の報告（補間入り等。localization ADR の既知の制約）
    public readonly struct ScanIssue
    {
        public readonly int LineNumber;
        public readonly string Reason;
        public ScanIssue(int lineNumber, string reason)
        {
            LineNumber = lineNumber;
            Reason = reason;
        }
    }

    public sealed class ScanResult
    {
        public readonly List<ScannedText> Texts = new();
        public readonly List<ScanIssue> Issues = new();
    }

    /// <summary>
    /// .rb シナリオの静的走査（追跡抽出パイプラインの入口・localization-unity-package ADR）。
    /// say / narration / choose / chara 糖衣呼び出し / as: (display_as:) の文字列リテラルを
    /// 出現順に抽出する。Ruby の完全パースはせず行ベースの best-effort（糖衣の間接呼びや動的
    /// 組み立ての漏れは LocalizedTableTextResolver.TextMissed の dev 収集で回収する）。
    ///
    /// 対象外（抽出しない）:
    /// - `#{}` 補間入りの二重引用符リテラル（実行時の最終文字列がキーになるため対象外。Issue として報告）
    /// - say の話者 id（カタログ id が普通。その場話者のリテラル表示は dev 収集で回収）
    /// - bg/se/portrait 等のアセットキー引数・`cmd :xxx` 直呼び（ライブラリ配管）
    /// </summary>
    public static class ScenarioTextScanner
    {
        // 第 1 パス: chara :alice 宣言を集める（糖衣メソッド名の解決に使う。ファイル横断で共有）
        public static void CollectCharaDeclarations(string source, ISet<string> charaSet)
        {
            foreach (var (line, _) in LogicalLines(source))
            {
                var name = ReadLeadingIdentifier(line, out var rest);
                if (name != "chara") continue;
                // chara :alice / chara "alice" / chara(:alice)
                var symbol = ReadFirstSymbolOrString(rest);
                if (!string.IsNullOrEmpty(symbol)) charaSet.Add(symbol!);
            }
        }

        // 第 2 パス: 本文抽出。charaSet は全ファイルの宣言を集めた後に渡す
        public static ScanResult Scan(string source, IReadOnlyCollection<string> charaSet)
        {
            var result = new ScanResult();
            var charaLookup = new HashSet<string>(charaSet);

            foreach (var (line, lineNumber) in LogicalLines(source))
            {
                var method = ReadLeadingIdentifier(line, out var rest);
                if (method == null) continue;

                var literals = ReadStringLiterals(rest, lineNumber, result.Issues);
                if (literals.Count == 0) continue;

                switch (method)
                {
                    case "narration":
                        AddPositional(result, literals, textIndex: 0, lineNumber);
                        AddKwarg(result, literals, "as", "display_as");
                        break;
                    case "say":
                        // say "text"（ナレーション形）/ say "id", "text"(, "portrait_key")。
                        // 位置引数の 2 つ目が本文（1 つしか無ければそれが本文）。話者 id は抽出しない
                        var positional = literals.FindAll(l => l.Kwarg == null);
                        var textIdx = positional.Count >= 2 ? 1 : 0;
                        if (positional.Count > textIdx) Add(result, positional[textIdx], lineNumber);
                        AddKwarg(result, literals, "as", "display_as");
                        break;
                    case "choose":
                        // choose(["A", "B"], key: :x) — key: 以外の位置リテラルが選択肢
                        foreach (var literal in literals)
                            if (literal.Kwarg == null)
                                Add(result, literal, lineNumber);
                        break;
                    case "cmd":
                        break;   // ライブラリ配管（preamble 内部）は対象外
                    default:
                        // chara 糖衣: alice "text"(, as: "…")。宣言済みメソッド名のみ拾う
                        if (charaLookup.Contains(method))
                        {
                            var pos = literals.FindAll(l => l.Kwarg == null);
                            if (pos.Count > 0) Add(result, pos[0], lineNumber);
                            AddKwarg(result, literals, "as", "display_as");
                        }
                        break;
                }
            }
            return result;
        }

        private static void Add(ScanResult result, Literal literal, int lineNumber)
        {
            if (literal.HasInterpolation)
            {
                result.Issues.Add(new ScanIssue(lineNumber,
                    "Ruby 補間 #{} 入りのため抽出できません。変数はテキスト変数 %{key} で書き直してください (訳の取得後に展開されるため多言語・既読と両立します)"));
                return;
            }
            if (literal.Text.Length > 0) result.Texts.Add(new ScannedText(literal.Text, lineNumber));
        }

        private static void AddPositional(ScanResult result, List<Literal> literals, int textIndex, int lineNumber)
        {
            var positional = literals.FindAll(l => l.Kwarg == null);
            if (positional.Count > textIndex) Add(result, positional[textIndex], lineNumber);
        }

        private static void AddKwarg(ScanResult result, List<Literal> literals, params string[] kwargNames)
        {
            foreach (var literal in literals)
                if (literal.Kwarg != null && System.Array.IndexOf(kwargNames, literal.Kwarg) >= 0)
                    Add(result, literal, literal.LineNumber);
        }

        // ---- 行の論理結合（choose の複数行配列などの継続行を括弧バランスで繋ぐ） ----

        private static IEnumerable<(string Line, int LineNumber)> LogicalLines(string source)
        {
            var lines = source.Replace("\r\n", "\n").Split('\n');
            var buffer = new StringBuilder();
            var startLine = 0;
            var depth = 0;

            for (var i = 0; i < lines.Length; i++)
            {
                var stripped = StripComment(lines[i]);
                if (buffer.Length == 0)
                {
                    if (stripped.Trim().Length == 0) continue;
                    startLine = i + 1;
                }
                else
                {
                    buffer.Append(' ');
                }
                buffer.Append(stripped);
                depth += BracketBalance(stripped);

                // 継続の暴走を防ぐ (未クローズの括弧が長く続く場合は諦めてその行までで確定)
                if (depth > 0 && i + 1 < lines.Length && (i + 1) - (startLine - 1) < 20) continue;

                yield return (buffer.ToString(), startLine);
                buffer.Clear();
                depth = 0;
            }
            if (buffer.Length > 0) yield return (buffer.ToString(), startLine);
        }

        // 文字列リテラル内を除いた # 以降（コメント）を落とす
        private static string StripComment(string line)
        {
            char quote = '\0';
            for (var i = 0; i < line.Length; i++)
            {
                var c = line[i];
                if (quote != '\0')
                {
                    if (c == '\\') i++;                 // エスケープの次文字はスキップ
                    else if (c == quote) quote = '\0';
                }
                else if (c == '\'' || c == '"') quote = c;
                else if (c == '#') return line.Substring(0, i);
            }
            return line;
        }

        private static int BracketBalance(string line)
        {
            var balance = 0;
            char quote = '\0';
            for (var i = 0; i < line.Length; i++)
            {
                var c = line[i];
                if (quote != '\0')
                {
                    if (c == '\\') i++;
                    else if (c == quote) quote = '\0';
                }
                else if (c == '\'' || c == '"') quote = c;
                else if (c is '(' or '[' or '{') balance++;
                else if (c is ')' or ']' or '}') balance--;
            }
            return balance;
        }

        // ---- 行内の要素読み取り ----

        private static string? ReadLeadingIdentifier(string line, out string rest)
        {
            var i = 0;
            while (i < line.Length && char.IsWhiteSpace(line[i])) i++;
            var start = i;
            while (i < line.Length && (char.IsLetterOrDigit(line[i]) || line[i] == '_')) i++;
            if (i == start) { rest = line; return null; }
            // 代入形 (n = choose(...)) は識別子 = の右辺を本体として読み直す
            var j = i;
            while (j < line.Length && char.IsWhiteSpace(line[j])) j++;
            if (j < line.Length && line[j] == '=' && (j + 1 >= line.Length || line[j + 1] != '='))
                return ReadLeadingIdentifier(line.Substring(j + 1), out rest);
            rest = line.Substring(i);
            return line.Substring(start, i - start);
        }

        private static string? ReadFirstSymbolOrString(string rest)
        {
            for (var i = 0; i < rest.Length; i++)
            {
                var c = rest[i];
                if (char.IsWhiteSpace(c) || c == '(') continue;
                if (c == ':')
                {
                    var start = ++i;
                    while (i < rest.Length && (char.IsLetterOrDigit(rest[i]) || rest[i] == '_')) i++;
                    return i > start ? rest.Substring(start, i - start) : null;
                }
                if (c == '\'' || c == '"')
                {
                    var end = rest.IndexOf(c, i + 1);
                    return end > i ? rest.Substring(i + 1, end - i - 1) : null;
                }
                return null;
            }
            return null;
        }

        private readonly struct Literal
        {
            public readonly string Text;
            public readonly string? Kwarg;          // 直前の kwarg 名 (display_as: 等)。位置引数は null
            public readonly bool HasInterpolation;
            public readonly int LineNumber;
            public Literal(string text, string? kwarg, bool hasInterpolation, int lineNumber)
            {
                Text = text;
                Kwarg = kwarg;
                HasInterpolation = hasInterpolation;
                LineNumber = lineNumber;
            }
        }

        // 行内の文字列リテラルを、直前の kwarg 名付きで出現順に読む
        private static List<Literal> ReadStringLiterals(string line, int lineNumber, List<ScanIssue> issues)
        {
            var literals = new List<Literal>();
            string? pendingKwarg = null;
            for (var i = 0; i < line.Length; i++)
            {
                var c = line[i];
                if (char.IsLetter(c) || c == '_')
                {
                    // 識別子を読み、直後が ':' なら kwarg 名として保持 (:: や三項の : と区別するため直後判定のみ)
                    var start = i;
                    while (i < line.Length && (char.IsLetterOrDigit(line[i]) || line[i] == '_')) i++;
                    if (i < line.Length && line[i] == ':' && (i + 1 >= line.Length || line[i + 1] != ':'))
                        pendingKwarg = line.Substring(start, i - start);
                    else
                    {
                        pendingKwarg = null;
                        i--;
                    }
                }
                else if (c == '\'' || c == '"')
                {
                    var (text, hasInterpolation, next) = ReadLiteral(line, i);
                    if (next < 0)
                    {
                        issues.Add(new ScanIssue(lineNumber, "閉じられていない文字列リテラル"));
                        break;
                    }
                    literals.Add(new Literal(text, pendingKwarg, hasInterpolation, lineNumber));
                    pendingKwarg = null;
                    i = next;
                }
                else if (!char.IsWhiteSpace(c) && c != '(' && c != '[' && c != ',')
                {
                    // シンボル・数値等の別トークンが挟まったら kwarg の効力は切れる
                    if (c == ':')
                    {
                        while (i + 1 < line.Length && (char.IsLetterOrDigit(line[i + 1]) || line[i + 1] == '_')) i++;
                    }
                    pendingKwarg = null;
                }
            }
            return literals;
        }

        // 二重引用符リテラルのエスケープ 1 つを解釈して sb へ足す。i はエスケープ文字 (\ の次) を指し、
        // 消費した最終文字の位置へ進めて返す。単純置換・8進 (\nnn)・16進 (\xNN)・Unicode (\uNNNN / \u{...}) に対応。
        // 未知のエスケープは Ruby 同様に文字だけ残す
        private static void AppendDoubleQuoteEscape(string line, ref int i, StringBuilder sb)
        {
            var e = line[i];
            switch (e)
            {
                case 'n': sb.Append('\n'); return;
                case 't': sb.Append('\t'); return;
                case 'r': sb.Append('\r'); return;
                case 'f': sb.Append('\f'); return;
                case 'v': sb.Append('\v'); return;
                case 'a': sb.Append('\a'); return;
                case 'b': sb.Append('\b'); return;
                case 'e': sb.Append('\x1b'); return;
                case 's': sb.Append(' '); return;
                case 'x':
                {
                    var value = 0;
                    var digits = 0;
                    while (digits < 2 && i + 1 < line.Length && IsHex(line[i + 1]))
                    {
                        value = value * 16 + HexValue(line[++i]);
                        digits++;
                    }
                    if (digits > 0) sb.Append((char)value);
                    else sb.Append('x');   // \x に 16 進が続かない形は Ruby では構文エラー。best-effort で温存
                    return;
                }
                case 'u':
                {
                    if (i + 1 < line.Length && line[i + 1] == '{')
                    {
                        var close = line.IndexOf('}', i + 2);
                        if (close > i + 2 && TryAppendCodePoints(line, i + 2, close - i - 2, sb))
                        {
                            i = close;
                            return;
                        }
                    }
                    else if (i + 4 < line.Length && TryParseHex(line, i + 1, 4, out var cp4))
                    {
                        sb.Append(char.ConvertFromUtf32(cp4));
                        i += 4;
                        return;
                    }
                    sb.Append('u');   // 不成立は best-effort で温存
                    return;
                }
                default:
                    if (e >= '0' && e <= '7')
                    {
                        var value = e - '0';
                        var digits = 1;
                        while (digits < 3 && i + 1 < line.Length && line[i + 1] >= '0' && line[i + 1] <= '7')
                        {
                            value = value * 8 + (line[++i] - '0');
                            digits++;
                        }
                        sb.Append((char)value);
                    }
                    else sb.Append(e);   // \\ \" \' \# を含む「エスケープした文字そのもの」
                    return;
            }
        }

        // Ruby の \u{...} は空白区切りで複数コードポイントを許す (例: \u{3042 3044} → あい)。
        // 全部が有効なときだけ sb へ足して true (途中失敗で半端に書かない)
        private static bool TryAppendCodePoints(string text, int start, int length, StringBuilder sb)
        {
            var parts = text.Substring(start, length)
                .Split(new[] { ' ' }, System.StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 0) return false;
            var buffer = new StringBuilder();
            foreach (var part in parts)
            {
                if (!TryParseHex(part, 0, part.Length, out var cp)) return false;
                buffer.Append(char.ConvertFromUtf32(cp));
            }
            sb.Append(buffer);
            return true;
        }

        private static bool IsHex(char c) => c is >= '0' and <= '9' or >= 'a' and <= 'f' or >= 'A' and <= 'F';
        private static int HexValue(char c) => c <= '9' ? c - '0' : (char.ToLowerInvariant(c) - 'a') + 10;

        private static bool TryParseHex(string text, int start, int length, out int value)
        {
            value = 0;
            if (length <= 0 || length > 6 || start + length > text.Length) return false;
            for (var i = start; i < start + length; i++)
            {
                if (!IsHex(text[i])) return false;
                value = value * 16 + HexValue(text[i]);
            }
            // サロゲート域は単独のコードポイントとして不正 (ConvertFromUtf32 が投げる) ため不成立扱い
            return value <= 0x10FFFF && (value < 0xD800 || value > 0xDFFF);
        }

        // Ruby の一重/二重引用符リテラルを 1 つ読み、アンエスケープ済みテキストを返す。
        // 戻り値 next は閉じ引用符の位置 (-1 = 未クローズ)。二重引用符は #{ を補間として検出
        private static (string Text, bool HasInterpolation, int Next) ReadLiteral(string line, int openIndex)
        {
            var quote = line[openIndex];
            var sb = new StringBuilder();
            var interpolation = false;
            for (var i = openIndex + 1; i < line.Length; i++)
            {
                var c = line[i];
                if (c == '\\' && i + 1 < line.Length)
                {
                    i++;
                    if (quote == '\'')
                    {
                        // 一重引用符は \\ と \' のみエスケープ。他は \ ごと残る (Ruby 準拠)
                        var e = line[i];
                        if (e == '\\' || e == '\'') sb.Append(e);
                        else { sb.Append('\\'); sb.Append(e); }
                    }
                    else
                    {
                        // ランタイムが受け取る文字列と一致しないとキー照合が永久に外れるため、
                        // MRuby の二重引用符エスケープを網羅的に解釈する
                        AppendDoubleQuoteEscape(line, ref i, sb);
                    }
                }
                else if (c == quote) return (sb.ToString(), interpolation, i);
                else
                {
                    if (quote == '"' && c == '#' && i + 1 < line.Length && line[i + 1] == '{') interpolation = true;
                    sb.Append(c);
                }
            }
            return ("", false, -1);
        }
    }
}
