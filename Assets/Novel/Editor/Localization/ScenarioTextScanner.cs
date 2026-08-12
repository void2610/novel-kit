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
                var isChara = charaLookup.Contains(method);
                // cmd 直呼び (ライブラリ配管) と対象外メソッド (bg/se/flag 等) は触らない
                if (!isChara && method is not ("say" or "narration" or "choose")) continue;

                // 位置は「実引数の並び」で数える。文字列リテラルだけを数えると、シンボル話者
                // (say :carol, "やあ", "carol/wave") で本文の位置がずれて立ち絵キーを拾ってしまう
                var args = SplitArguments(rest, lineNumber, result.Issues);
                var positional = args.FindAll(a => a.Kwarg == null);

                switch (method)
                {
                    case "narration":
                        AddPositional(result, positional, index: 0, lineNumber);
                        break;
                    case "say":
                        // say "text"（ナレーション形）/ say speaker, "text"(, "portrait_key")。
                        // 位置引数の 2 つ目が本文（1 つしか無ければそれが本文）。話者 id は抽出しない
                        AddPositional(result, positional, positional.Count >= 2 ? 1 : 0, lineNumber);
                        // guest: true はカタログ外の単発キャラ。未登録 id は**そのまま表示名になる**
                        // （ITextResolver を通る）ため、話者リテラルも抽出対象にする
                        if (positional.Count >= 2 && HasTrueKwarg(args, "guest"))
                            AddPositional(result, positional, 0, lineNumber);
                        break;
                    case "choose":
                        // choose(["A", "B"], key: :x) — 位置引数（配列）内の全リテラルが選択肢
                        foreach (var arg in positional)
                            foreach (var literal in arg.Literals)
                                Add(result, literal, lineNumber);
                        break;
                    default:
                        // chara 糖衣: alice "text"(, as: "…")
                        AddPositional(result, positional, index: 0, lineNumber);
                        break;
                }

                // 表示名の上書き (say/chara 糖衣共通)
                foreach (var arg in args)
                    if (arg.Kwarg is "as" or "display_as" && arg.Value is { } displayName)
                        Add(result, displayName, lineNumber);
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

        // `name: true` 形の kwarg が付いているか（値が false / 変数のときは付いていない扱い）
        private static bool HasTrueKwarg(List<Argument> args, string name)
            => args.Exists(a => a.Kwarg == name && a.RawValue.Trim() == "true");

        // 指定位置の引数が文字列リテラルそのものなら抽出する（変数・メソッド呼び出しは対象外）
        private static void AddPositional(ScanResult result, List<Argument> positional, int index, int lineNumber)
        {
            if (index < positional.Count && positional[index].Value is { } literal)
                Add(result, literal, lineNumber);
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
            public readonly bool HasInterpolation;
            public Literal(string text, bool hasInterpolation)
            {
                Text = text;
                HasInterpolation = hasInterpolation;
            }
        }

        // 呼び出しの実引数 1 つ分。位置は実引数単位で数える (シンボル・変数も 1 つと数える)
        private readonly struct Argument
        {
            public readonly string? Kwarg;         // kwarg 名 (display_as: 等)。位置引数は null
            public readonly List<Literal> Literals;   // 引数内に現れた全リテラル (配列リテラル用)
            public readonly Literal? Value;        // 引数そのものが文字列リテラルならその値
            public readonly string RawValue;       // 値部分の生テキスト (true/false 等の判定用)
            public Argument(string? kwarg, List<Literal> literals, Literal? value, string rawValue)
            {
                Kwarg = kwarg;
                Literals = literals;
                Value = value;
                RawValue = rawValue;
            }
        }

        // 引数リストを最上位のカンマで分割する。文字列内・入れ子の括弧内のカンマは無視し、
        // 最上位の ')' で終端する
        private static List<Argument> SplitArguments(string rest, int lineNumber, List<ScanIssue> issues)
        {
            var args = new List<Argument>();
            var i = 0;
            while (i < rest.Length && char.IsWhiteSpace(rest[i])) i++;
            if (i < rest.Length && rest[i] == '(') i++;   // 括弧付き呼び出し

            var segmentStart = i;
            var depth = 0;
            while (i <= rest.Length)
            {
                if (i == rest.Length)
                {
                    AddSegment(args, rest, segmentStart, i, lineNumber, issues);
                    break;
                }
                var c = rest[i];
                if (c == '\'' || c == '"')
                {
                    var (_, _, next) = ReadLiteral(rest, i);
                    if (next < 0)
                    {
                        issues.Add(new ScanIssue(lineNumber, "閉じられていない文字列リテラル"));
                        break;
                    }
                    i = next + 1;
                    continue;
                }
                if (depth == 0 && c == ',')
                {
                    AddSegment(args, rest, segmentStart, i, lineNumber, issues);
                    segmentStart = i + 1;
                }
                else if (depth == 0 && c == ')')
                {
                    AddSegment(args, rest, segmentStart, i, lineNumber, issues);
                    return args;   // 呼び出しの終端 (後続の後置 if / メソッドチェーンは読まない)
                }
                else if (c is '(' or '[' or '{') depth++;
                else if (c is ')' or ']' or '}') depth--;
                i++;
            }
            return args;
        }

        private static void AddSegment(List<Argument> args, string rest, int start, int end,
            int lineNumber, List<ScanIssue> issues)
        {
            if (end <= start) return;
            var segment = rest.Substring(start, end - start);
            if (segment.Trim().Length == 0) return;
            args.Add(ParseArgument(segment, lineNumber, issues));
        }

        // 引数 1 つを解析する。`name: value` 形は kwarg として名前を落とし、値部分のリテラルを読む
        private static Argument ParseArgument(string segment, int lineNumber, List<ScanIssue> issues)
        {
            var i = 0;
            while (i < segment.Length && char.IsWhiteSpace(segment[i])) i++;

            string? kwarg = null;
            var identifierStart = i;
            while (i < segment.Length && (char.IsLetterOrDigit(segment[i]) || segment[i] == '_')) i++;
            // 識別子の直後が ':' で、シンボル (`:sym`) でも `::` でもなければ kwarg
            if (i > identifierStart && i < segment.Length && segment[i] == ':' &&
                (i + 1 >= segment.Length || segment[i + 1] != ':'))
            {
                kwarg = segment.Substring(identifierStart, i - identifierStart);
                i++;
            }
            else
            {
                i = identifierStart;
            }
            while (i < segment.Length && char.IsWhiteSpace(segment[i])) i++;

            // 値部分の全リテラルを読む。値そのものが文字列リテラルなら Value に立てる
            var literals = new List<Literal>();
            Literal? value = null;
            for (var j = i; j < segment.Length; j++)
            {
                if (segment[j] != '\'' && segment[j] != '"') continue;
                var (text, hasInterpolation, next) = ReadLiteral(segment, j);
                if (next < 0)
                {
                    issues.Add(new ScanIssue(lineNumber, "閉じられていない文字列リテラル"));
                    break;
                }
                var literal = new Literal(text, hasInterpolation);
                literals.Add(literal);
                if (j == i) value = literal;   // 引数の先頭が文字列 = 引数はリテラルそのもの
                j = next;
            }
            return new Argument(kwarg, literals, value, segment.Substring(i));
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
