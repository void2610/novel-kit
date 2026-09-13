#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Novel.Runtime;

namespace Novel.Editor
{
    /// <summary>
    /// コマンド語彙から糖衣 preamble の Ruby ソースを生成する (command-sugar-generation ADR)。
    /// 生成形は「宣言順の位置引数 (全て省略可) + **kw」で、nil でない引数だけを cmd へ渡す
    /// (未指定はデシリアライザの C# 既定値に任せる)。衝突・不正名は生成せず Skipped に理由を残す。
    /// </summary>
    internal static class CommandSugarGenerator
    {
        public sealed class Result
        {
            public string Source = "";
            public List<string> Generated = new();
            public List<string> Skipped = new();   // "名前 (理由)" 形式
        }

        // Ruby のメソッド名 / 引数名として裸で書ける形のみ許可 (?! 付き等は cmd 経由で書けばよい)
        private static readonly Regex Identifier = new("^[a-z_][a-zA-Z0-9_]*$", RegexOptions.Compiled);

        public static Result Generate(IReadOnlyList<CommandKeyInfo> commands, ISet<string> reservedNames)
        {
            var result = new Result();
            var sb = new StringBuilder();
            sb.AppendLine("# 自動生成: RegisterNovelCommandSugars がコマンド語彙から生成した糖衣。手で編集しない (再生時に上書きされる)。");
            sb.AppendLine("# 同名の def を自前の preamble に書けばそちらが後勝ちで有効になる。");

            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (var command in commands.OrderBy(c => c.Name, StringComparer.Ordinal))
            {
                if (!seen.Add(command.Name))
                {
                    result.Skipped.Add($"{command.Name} (複数モジュールが同名を登録)");
                    continue;
                }
                if (reservedNames.Contains(command.Name))
                {
                    result.Skipped.Add($"{command.Name} (組込の語彙・糖衣と衝突)");
                    continue;
                }
                if (!Identifier.IsMatch(command.Name))
                {
                    result.Skipped.Add($"{command.Name} (Ruby のメソッド名にできない)");
                    continue;
                }

                // 引数名が裸の識別子にできないコマンドは位置引数を諦め、**kw 委譲だけ生やす
                var parameters = command.Parameters.All(p => Identifier.IsMatch(p.Name))
                    ? command.Parameters
                    : Array.Empty<CommandParameterInfo>();

                sb.AppendLine();
                // def 直上コメントは Project Reference の糖衣一覧で説明として表示される
                sb.Append("# ").AppendLine(command.Description ?? $"{command.CommandType} ({command.ModuleType})");
                AppendDef(sb, command.Name, parameters);
                result.Generated.Add(command.Name);
            }
            result.Source = sb.ToString();
            return result;
        }

        private static void AppendDef(StringBuilder sb, string name, IReadOnlyList<CommandParameterInfo> parameters)
        {
            var defArgs = parameters.Select(p => $"{p.Name} = nil").Append("**kw");
            sb.Append("def ").Append(name).Append('(').Append(string.Join(", ", defArgs)).AppendLine(")");
            if (parameters.Count > 0)
            {
                sb.AppendLine("  h = {}");
                foreach (var p in parameters)
                    sb.Append("  h[:").Append(p.Name).Append("] = ").Append(p.Name)
                      .Append(" unless ").Append(p.Name).AppendLine(".nil?");
                sb.AppendLine("  kw.each { |k, v| h[k] = v }");
                sb.Append("  cmd :").Append(name).AppendLine(", **h");
            }
            else
            {
                sb.Append("  cmd :").Append(name).AppendLine(", **kw");
            }
            sb.AppendLine("end");
        }
    }
}
