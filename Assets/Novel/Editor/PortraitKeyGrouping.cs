#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;

namespace Novel.Editor
{
    /// <summary>プロジェクトリファレンスのキャラタブに並べる立ち絵 1 件。</summary>
    internal readonly struct PortraitKeyRow
    {
        /// <summary>シナリオにそのまま書けるキー。</summary>
        public string Key { get; }

        /// <summary>キャラを特定する部分を落とした表示用の短縮名 (コピー対象ではない)。</summary>
        public string ShortName { get; }

        /// <summary>カタログの既定立ち絵か。</summary>
        public bool IsDefault { get; }

        public PortraitKeyRow(string key, string shortName, bool isDefault)
        {
            Key = key;
            ShortName = shortName;
            IsDefault = isDefault;
        }
    }

    /// <summary>
    /// 「このキャラの立ち絵はどれか」を全キーの中から推定する純ロジック (project-reference ADR)。
    /// ランタイムにキャラ単位のキー名前空間は無いため、カタログが宣言した既定立ち絵の所在と
    /// キー中のキャラ id を手掛かりにした推定であり、あくまで一覧の便宜。
    /// </summary>
    internal static class PortraitKeyGrouping
    {
        /// <summary>
        /// 推定の優先順は「既定立ち絵と同じフォルダ」→「パスセグメントがキャラ id と一致」→
        /// 「ファイル名が id_ / id- で始まる」。既定立ち絵は実体が無くても必ず先頭に載せる
        /// (カタログの宣言はキーとして有効で、欠けていること自体が知りたい情報のため)。
        /// </summary>
        public static IReadOnlyList<PortraitKeyRow> Collect(
            string characterId, string? defaultPortraitKey, IEnumerable<string> allKeys)
        {
            var keys = allKeys as IReadOnlyCollection<string> ?? allKeys.ToList();
            var defaultKey = string.IsNullOrEmpty(defaultPortraitKey) ? null : defaultPortraitKey;

            var scope = FolderOf(defaultKey);
            var matched = scope == null
                ? null
                : keys.Where(k => k.Length > scope.Length && k.StartsWith(scope, StringComparison.Ordinal)).ToList();

            if (matched is not { Count: > 0 })
            {
                scope = null;
                matched = keys.Where(k => HasSegment(k, characterId)).ToList();
            }
            if (matched.Count == 0)
                matched = keys.Where(k => FileNameHasIdPrefix(k, characterId)).ToList();

            var rows = matched
                .OrderBy(k => k, StringComparer.Ordinal)
                .Select(k => new PortraitKeyRow(k, Shorten(k, scope, characterId), k == defaultKey))
                .ToList();

            if (defaultKey != null && rows.All(r => r.Key != defaultKey))
                rows.Insert(0, new PortraitKeyRow(defaultKey, Shorten(defaultKey, scope, characterId), true));
            return rows;
        }

        /// <summary>末尾の '/' まで含むフォルダ部分 (階層が無ければ null)。</summary>
        private static string? FolderOf(string? key)
        {
            if (key == null) return null;
            var slash = key.LastIndexOf('/');
            return slash < 0 ? null : key.Substring(0, slash + 1);
        }

        private static bool HasSegment(string key, string id)
        {
            if (string.IsNullOrEmpty(id)) return false;
            foreach (var segment in key.Split('/'))
                if (string.Equals(segment, id, StringComparison.OrdinalIgnoreCase))
                    return true;
            return false;
        }

        private static bool FileNameHasIdPrefix(string key, string id)
        {
            if (string.IsNullOrEmpty(id)) return false;
            var name = FileNameOf(key);
            return name.Length > id.Length + 1
                   && name.StartsWith(id, StringComparison.OrdinalIgnoreCase)
                   && (name[id.Length] == '_' || name[id.Length] == '-');
        }

        /// <summary>キャラを特定する部分 (共通フォルダ / id セグメント / id_ 接頭辞) を落とす。</summary>
        private static string Shorten(string key, string? scope, string id)
        {
            if (scope != null && key.StartsWith(scope, StringComparison.Ordinal))
                return key.Substring(scope.Length);

            var segments = key.Split('/');
            for (var i = 0; i < segments.Length; i++)
            {
                if (!string.Equals(segments[i], id, StringComparison.OrdinalIgnoreCase)) continue;
                // id 自体が末尾セグメントなら落とすものが無い (短縮せずファイル名を返す)
                if (i < segments.Length - 1) return string.Join("/", segments.Skip(i + 1));
                break;
            }

            var name = FileNameOf(key);
            return FileNameHasIdPrefix(key, id) ? name.Substring(id.Length + 1) : name;
        }

        private static string FileNameOf(string key)
        {
            var slash = key.LastIndexOf('/');
            return slash < 0 ? key : key.Substring(slash + 1);
        }
    }
}
