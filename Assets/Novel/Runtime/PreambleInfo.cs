#nullable enable
using System.Collections.Generic;

namespace Novel.Runtime
{
    /// <summary>
    /// 再生時に読み込んだ preamble 1 つの目録 (project-reference ADR)。エディタは <see cref="BytecodeHash"/> で
    /// 元の <c>.rb</c> アセット (ScriptedImporter が <c>.mrb</c> サブアセットを生やしたもの) を特定し、
    /// ソースから引数名・既定値・コメントを読む。見つからなければ <see cref="MethodNames"/> だけを出す。
    /// </summary>
    public sealed class PreambleInfo
    {
        public string SourceType { get; }

        /// <summary>バイトコードの SHA-1 (16 進)。</summary>
        public string BytecodeHash { get; }

        /// <summary>この preamble が新たに Object へ定義したメソッド名 (糖衣)。</summary>
        public IReadOnlyList<string> MethodNames { get; }

        public PreambleInfo(string sourceType, string bytecodeHash, IReadOnlyList<string> methodNames)
        {
            SourceType = sourceType;
            BytecodeHash = bytecodeHash;
            MethodNames = methodNames;
        }
    }

    /// <summary><see cref="IWorldEffectSink"/> が解釈できる world_effect キーの目録エントリ。</summary>
    public readonly struct WorldEffectKeyInfo
    {
        public string Key { get; }

        /// <summary>ライター向けメモ (任意)。引数の意味など。</summary>
        public string? Note { get; }

        public WorldEffectKeyInfo(string key, string? note = null)
        {
            Key = key;
            Note = note;
        }
    }
}
