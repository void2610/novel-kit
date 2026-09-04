#nullable enable
using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using UnityEditor;
using UnityEngine;

namespace Novel.Editor
{
    /// <summary>
    /// プロジェクト内の <c>.rb</c> アセットを、ScriptedImporter が生やした <c>.mrb</c> サブアセットのハッシュで引く。
    /// 再生時キャプチャ (<see cref="Novel.Runtime.PreambleInfo.BytecodeHash"/>) と突き合わせて元ソースを特定するため。
    /// </summary>
    internal static class PreambleSourceCatalog
    {
        public sealed class Entry
        {
            public string AssetPath = "";
            public string Source = "";
        }

        private static Dictionary<string, Entry>? _byHash;

        public static void Invalidate() => _byHash = null;

        public static Entry? Find(string bytecodeHash)
        {
            _byHash ??= Scan();
            return _byHash.TryGetValue(bytecodeHash, out var entry) ? entry : null;
        }

        private static Dictionary<string, Entry> Scan()
        {
            var result = new Dictionary<string, Entry>(StringComparer.Ordinal);
            foreach (var guid in AssetDatabase.FindAssets("t:TextAsset"))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                if (!path.EndsWith(".rb", StringComparison.Ordinal)) continue;
                string? source = null;
                byte[]? bytecode = null;
                foreach (var asset in AssetDatabase.LoadAllAssetsAtPath(path))
                {
                    if (asset is not TextAsset text) continue;
                    if (text.name.EndsWith(".mrb", StringComparison.Ordinal)) bytecode = text.bytes;
                    else source = text.text;
                }
                if (source == null || bytecode == null) continue;
                result[Sha1Hex(bytecode)] = new Entry { AssetPath = path, Source = source };
            }
            return result;
        }

        private static string Sha1Hex(byte[] bytes)
        {
            using var sha = SHA1.Create();
            var hash = sha.ComputeHash(bytes);
            var sb = new System.Text.StringBuilder(hash.Length * 2);
            foreach (var b in hash) sb.Append(b.ToString("x2"));
            return sb.ToString();
        }
    }
}
