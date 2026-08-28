#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using Void2610.CinematicEffect;

namespace Novel.Cinematic.Editor
{
    /// <summary>配置規約 <c>Resources/Novel/Effects/</c> を走査し、シナリオから呼べる演出キーを列挙する。</summary>
    internal static class CinematicEffectCatalog
    {
        public sealed class Entry
        {
            public string Key = "";
            public string AssetPath = "";
            public int StepCount;
            public string ExitKind = "";   // 専用 / 導出 / なし
        }

        private const string Marker = "/Resources/" + ResourcesCinematicSequenceLoader.Root;

        public static List<Entry> Scan()
        {
            var pathByKey = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var guid in AssetDatabase.FindAssets("t:CinematicSequenceAsset"))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var at = path.IndexOf(Marker, StringComparison.Ordinal);
                if (at < 0 || path.Contains("/Tests/") || path.Contains("/Editor/")) continue;
                var relative = path.Substring(at + Marker.Length);
                var dot = relative.LastIndexOf('.');
                pathByKey[dot >= 0 ? relative.Substring(0, dot) : relative] = path;
            }

            var entries = new List<Entry>();
            foreach (var (key, path) in pathByKey)
            {
                if (key.EndsWith(CinematicCommandModule.ExitSuffix, StringComparison.Ordinal)) continue;   // Exit は Enter 側の行に畳む
                var asset = AssetDatabase.LoadAssetAtPath<CinematicSequenceAsset>(path);
                if (asset == null) continue;
                var exitKind = pathByKey.ContainsKey(key + CinematicCommandModule.ExitSuffix) ? "専用"
                    : CinematicExitDeriver.Derive(asset) != null ? "導出"
                    : "なし (一発物)";
                entries.Add(new Entry { Key = key, AssetPath = path, StepCount = asset.steps.Count, ExitKind = exitKind });
            }
            return entries.OrderBy(e => e.Key, StringComparer.Ordinal).ToList();
        }

        public static HashSet<string> KnownKeys() => new(Scan().Select(e => e.Key), StringComparer.Ordinal);
    }
}
