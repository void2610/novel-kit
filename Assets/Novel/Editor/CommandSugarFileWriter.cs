#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Novel.Runtime;
using UnityEditor;
using UnityEngine;

namespace Novel.Editor
{
    /// <summary>
    /// RegisterNovelCommandSugars の語彙キャプチャを購読し、糖衣 preamble を
    /// Assets/Resources/Novel/CommandSugars.rb へ書き出す (command-sugar-generation ADR)。
    /// 内容が変わったときだけ書き、生成物が効くのは import 後の次の再生から。
    /// </summary>
    [InitializeOnLoad]
    internal static class CommandSugarFileWriter
    {
        internal const string AssetPath = "Assets/Resources/" + NovelCommandSugars.ResourceKey + ".rb";

        static CommandSugarFileWriter()
        {
            NovelCommandSugars.Captured += OnCaptured;
        }

        private static void OnCaptured(IReadOnlyList<CommandKeyInfo> commands)
        {
            // 語彙ゼロの配線 (novel 未配線スコープ等) では既存の生成物を消さない (キャプチャの「空 = 未提供」と同じ方針)
            if (commands.Count == 0) return;
            // Play Mode 突入中の import を避け、エディタが落ち着いてから書く
            EditorApplication.delayCall += () => Write(commands);
        }

        private static void Write(IReadOnlyList<CommandKeyInfo> commands)
        {
            try
            {
                var result = CommandSugarGenerator.Generate(commands, ReservedNames());
                foreach (var skipped in result.Skipped)
                    Debug.LogWarning($"[Novel] 糖衣を生成できません: {skipped}");

                var current = File.Exists(AssetPath) ? File.ReadAllText(AssetPath) : null;
                if (current == result.Source) return;

                Directory.CreateDirectory(Path.GetDirectoryName(AssetPath)!);
                File.WriteAllText(AssetPath, result.Source);
                AssetDatabase.ImportAsset(AssetPath);
                Debug.Log($"[Novel] 糖衣 preamble を生成しました ({result.Generated.Count} 件): {AssetPath}。次の再生から有効です");
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[Novel] 糖衣 preamble の生成に失敗: {e}");
            }
        }

        /// <summary>組込語彙 + 組込 preamble の def 名。生成糖衣が上書きしてはいけない集合。</summary>
        internal static ISet<string> ReservedNames()
        {
            var reserved = new HashSet<string>(NovelCommandSugars.BuiltinCommandNames, StringComparer.Ordinal);
            // 組込 preamble (Novel/Preamble.rb) の糖衣名はソースから読む (追加されても追随する)
            var builtin = Resources.Load<TextAsset>("Novel/Preamble");
            if (builtin != null)
                foreach (var def in RubyDefParser.Parse(builtin.text))
                    reserved.Add(def.Name);
            return reserved;
        }
    }
}
