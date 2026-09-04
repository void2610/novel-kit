#nullable enable
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using VitalRouter;

namespace Novel.Runtime
{
    /// <summary>
    /// Ruby 名と C# コマンド型を束ねる口。<see cref="INovelCommandModule.RegisterVocabulary"/> が受け取る。
    /// 実体は runner が MRubyState へ委譲する実装だが、エディタ (プロジェクトリファレンス) は記録専用の実装を渡して
    /// MRubyState を作らずに語彙を読む。VitalRouter.MRuby に登録済み語彙を読み戻す API が無いため、束縛口を novel-kit 側で持つ。
    /// </summary>
    public interface INovelVocabulary
    {
        /// <remarks>説明はコマンド型・プロパティの <see cref="NovelDescriptionAttribute"/> で付ける。</remarks>
        void Add<TCommand>(string rubyName) where TCommand : ICommand;
    }

    /// <summary>プロジェクト定義コマンド 1 つの目録 (project-reference ADR)。</summary>
    public sealed class CommandKeyInfo
    {
        /// <summary><c>cmd :name</c> の name。</summary>
        public string Name { get; }
        public string CommandType { get; }
        public string ModuleType { get; }
        public IReadOnlyList<CommandParameterInfo> Parameters { get; }

        /// <summary>コマンド型の <see cref="NovelDescriptionAttribute"/> (無ければ null)。</summary>
        public string? Description { get; }

        public CommandKeyInfo(string name, string commandType, string moduleType, IReadOnlyList<CommandParameterInfo> parameters,
            string? description = null)
        {
            Description = string.IsNullOrEmpty(description) ? null : description;
            Name = name;
            CommandType = commandType;
            ModuleType = moduleType;
            Parameters = parameters;
        }
    }

    public readonly struct CommandParameterInfo
    {
        /// <summary><c>cmd :name, key: value</c> の key (Ruby 側の名前)。</summary>
        public string Name { get; }
        public string TypeName { get; }

        /// <summary>プロパティの <see cref="NovelDescriptionAttribute"/> (無ければ null)。</summary>
        public string? Description { get; }

        public CommandParameterInfo(string name, string typeName, string? description = null)
        {
            Name = name;
            TypeName = typeName;
            Description = string.IsNullOrEmpty(description) ? null : description;
        }
    }

    /// <summary>
    /// 語彙を記録するだけの <see cref="INovelVocabulary"/>。引数は <c>[MRubyObject]</c> 型のプロパティから
    /// MRubyCS.Serializer と同じ規則 (snake_case・<c>[MRubyMember]</c> で上書き・<c>[MRubyIgnore]</c> で除外) で読む。
    /// </summary>
    public sealed class RecordingVocabulary : INovelVocabulary
    {
        private readonly List<CommandKeyInfo> _commands = new();
        private readonly string _moduleType;

        public RecordingVocabulary(string moduleType)
        {
            _moduleType = moduleType;
        }

        public IReadOnlyList<CommandKeyInfo> Commands => _commands;

        public void Add<TCommand>(string rubyName) where TCommand : ICommand =>
            _commands.Add(new CommandKeyInfo(rubyName, typeof(TCommand).Name, _moduleType, DescribeParameters(typeof(TCommand)),
                Description(typeof(TCommand))));

        private static string? Description(MemberInfo member) =>
            (Attribute.GetCustomAttribute(member, typeof(NovelDescriptionAttribute)) as NovelDescriptionAttribute)?.Text;

        public static IReadOnlyList<CommandParameterInfo> DescribeParameters(Type commandType)
        {
            var result = new List<CommandParameterInfo>();
            // GetProperties は宣言順を保証しないため、宣言順に対応する MetadataToken で固定する (コピー雛形が宣言順前提)
            var properties = commandType.GetProperties(BindingFlags.Public | BindingFlags.Instance);
            Array.Sort(properties, (a, b) => a.MetadataToken.CompareTo(b.MetadataToken));
            foreach (var property in properties)
            {
                if (!property.CanRead || property.GetIndexParameters().Length > 0) continue;
                if (HasAttribute(property, "MRubyIgnoreAttribute")) continue;
                var name = MemberNameOverride(property) ?? ToSnakeCase(property.Name);
                result.Add(new CommandParameterInfo(name, FriendlyTypeName(property.PropertyType), Description(property)));
            }
            return result;
        }

        // MRubyCS.Serializer への参照を Runtime に持ち込まないため属性は名前で見る
        private static bool HasAttribute(MemberInfo member, string attributeName)
        {
            foreach (var attribute in member.GetCustomAttributes(true))
                if (attribute.GetType().Name == attributeName) return true;
            return false;
        }

        private static string? MemberNameOverride(MemberInfo member)
        {
            foreach (var attribute in member.GetCustomAttributes(true))
            {
                if (attribute.GetType().Name != "MRubyMemberAttribute") continue;
                return attribute.GetType().GetProperty("Name")?.GetValue(attribute) as string;
            }
            return null;
        }

        public static string ToSnakeCase(string name)
        {
            var sb = new StringBuilder(name.Length + 4);
            for (var i = 0; i < name.Length; i++)
            {
                var c = name[i];
                if (char.IsUpper(c))
                {
                    if (i > 0) sb.Append('_');
                    sb.Append(char.ToLowerInvariant(c));
                }
                else
                {
                    sb.Append(c);
                }
            }
            return sb.ToString();
        }

        private static string FriendlyTypeName(Type type)
        {
            var underlying = Nullable.GetUnderlyingType(type);
            if (underlying != null) return FriendlyTypeName(underlying) + "?";
            if (type.IsArray) return FriendlyTypeName(type.GetElementType()!) + "[]";
            return type switch
            {
                _ when type == typeof(string) => "string",
                _ when type == typeof(int) => "int",
                _ when type == typeof(long) => "long",
                _ when type == typeof(float) => "float",
                _ when type == typeof(double) => "double",
                _ when type == typeof(bool) => "bool",
                _ => type.Name,
            };
        }
    }
}
