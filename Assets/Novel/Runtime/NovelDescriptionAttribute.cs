#nullable enable
using System;

namespace Novel.Runtime
{
    /// <summary>
    /// プロジェクト定義コマンドの説明 (project-reference ADR)。コマンド型に付ければコマンドの説明、
    /// プロパティに付ければその引数の説明として、プロジェクトリファレンスの「コマンド」タブに出る。
    /// ライターが読む前提で書く。実行には関与しない。
    /// </summary>
    [AttributeUsage(AttributeTargets.Struct | AttributeTargets.Class | AttributeTargets.Property)]
    public sealed class NovelDescriptionAttribute : Attribute
    {
        public string Text { get; }
        public NovelDescriptionAttribute(string text) => Text = text;
    }
}
