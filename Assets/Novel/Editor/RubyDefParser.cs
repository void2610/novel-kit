#nullable enable
using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;

namespace Novel.Editor
{
    /// <summary>preamble の <c>.rb</c> ソースから読んだ糖衣 1 つ。</summary>
    internal sealed class RubyDef
    {
        public sealed class Param
        {
            public string Name = "";
            public string? Default;        // 省略可なら既定値のソース文字列
            public bool IsKeyword;
            public bool IsRest;            // *args / &block
        }

        public string Name = "";
        public List<Param> Params = new();
        public string? Comment;            // def 直上のコメント行 (連続する # 行を結合)

        /// <summary>そのまま .rb に書ける呼び出し形。必須引数は名前をプレースホルダーにし、省略可は既定値を置く。</summary>
        public string CallTemplate()
        {
            var args = new List<string>();
            foreach (var p in Params)
            {
                if (p.IsRest) continue;
                if (p.IsKeyword) args.Add($"{p.Name}: {p.Default ?? "nil"}");
                else args.Add(p.Default ?? p.Name);
            }
            return args.Count == 0 ? Name : $"{Name} {string.Join(", ", args)}";
        }

        /// <summary>一覧表示用の署名。</summary>
        public string Signature()
        {
            var parts = new List<string>();
            foreach (var p in Params)
            {
                if (p.IsRest) parts.Add(p.Name);
                else if (p.IsKeyword) parts.Add(p.Default == null ? $"{p.Name}:" : $"{p.Name}: {p.Default}");
                else parts.Add(p.Default == null ? p.Name : $"{p.Name} = {p.Default}");
            }
            return parts.Count == 0 ? Name : $"{Name}({string.Join(", ", parts)})";
        }
    }

    /// <summary>
    /// <c>.rb</c> ソースの最上位 <c>def</c> を正規表現で拾う (Ruby パーサは持たない)。バイトコードにはデバッグ情報が
    /// 無く引数名を取れないため、ソースが残っている場合の補完として使う。入れ子の def (class_eval 内等) は対象外。
    /// </summary>
    internal static class RubyDefParser
    {
        private static readonly Regex DefLine = new(@"^def\s+([A-Za-z_][A-Za-z0-9_]*[?!]?)\s*(?:\((.*)\)|(.*))?\s*$", RegexOptions.Compiled);

        public static List<RubyDef> Parse(string source)
        {
            var defs = new List<RubyDef>();
            var comment = new StringBuilder();
            foreach (var raw in source.Replace("\r\n", "\n").Split('\n'))
            {
                var line = raw.TrimEnd();
                if (line.StartsWith("#", StringComparison.Ordinal))
                {
                    if (comment.Length > 0) comment.Append(' ');
                    comment.Append(line.TrimStart('#').Trim());
                    continue;
                }
                var match = DefLine.Match(line);
                if (match.Success)
                {
                    var def = new RubyDef { Name = match.Groups[1].Value, Comment = comment.Length > 0 ? comment.ToString() : null };
                    var paramText = match.Groups[2].Success ? match.Groups[2].Value : match.Groups[3].Value;
                    ParseParams(paramText, def.Params);
                    defs.Add(def);
                }
                // 直上以外のコメントは説明ではない (空行や別の行を挟んだら捨てる)
                comment.Clear();
            }
            return defs;
        }

        private static void ParseParams(string text, List<RubyDef.Param> into)
        {
            foreach (var piece in SplitTopLevel(text))
            {
                var p = piece.Trim();
                if (p.Length == 0) continue;
                if (p.StartsWith("*", StringComparison.Ordinal) || p.StartsWith("&", StringComparison.Ordinal))
                {
                    into.Add(new RubyDef.Param { Name = p, IsRest = true });
                    continue;
                }
                var colon = p.IndexOf(':');
                var eq = p.IndexOf('=');
                if (colon > 0 && (eq < 0 || colon < eq))
                {
                    var value = p.Substring(colon + 1).Trim();
                    into.Add(new RubyDef.Param { Name = p.Substring(0, colon).Trim(), IsKeyword = true, Default = value.Length == 0 ? null : value });
                }
                else if (eq > 0)
                {
                    into.Add(new RubyDef.Param { Name = p.Substring(0, eq).Trim(), Default = p.Substring(eq + 1).Trim() });
                }
                else
                {
                    into.Add(new RubyDef.Param { Name = p });
                }
            }
        }

        // 既定値に配列やハッシュ (カンマ入り) があっても壊れないよう、括弧の深さ 0 のカンマだけで区切る
        private static IEnumerable<string> SplitTopLevel(string text)
        {
            var depth = 0;
            var start = 0;
            var quote = '\0';
            for (var i = 0; i < text.Length; i++)
            {
                var c = text[i];
                if (quote != '\0')
                {
                    if (c == quote) quote = '\0';
                    continue;
                }
                switch (c)
                {
                    case '\'': case '"': quote = c; break;
                    case '(': case '[': case '{': depth++; break;
                    case ')': case ']': case '}': depth--; break;
                    case ',' when depth == 0:
                        yield return text.Substring(start, i - start);
                        start = i + 1;
                        break;
                }
            }
            yield return text.Substring(start);
        }
    }
}
