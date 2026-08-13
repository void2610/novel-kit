#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using Novel.Editor.Localization;

namespace Novel.Tests
{
    /// <summary>
    /// <see cref="ITextTableEditor"/> のインメモリ実装（抽出の適用ロジック検証用）。
    /// 実テーブル同様にエントリへ安定 ID を振り、<see cref="RenameKey"/> でも維持する。
    /// これにより「リネームで訳・メタデータが同じエントリに追従するか」をテストで確認できる。
    /// </summary>
    public sealed class FakeTextTable : ITextTableEditor
    {
        private sealed class Entry
        {
            public int Id;
            public string Key = "";
            public readonly Dictionary<string, string> Values = new();
            public readonly List<TextSourceRef> Sources = new();
            public (string Reason, string PreviousSource)? Fuzzy;
            public bool Deprecated;
            public readonly List<(string PreviousSource, string Locale, string Value)> Archived = new();
        }

        private readonly List<Entry> _entries = new();
        private readonly List<string> _locales;
        private int _nextId = 1;

        public int SaveCount { get; private set; }

        public FakeTextTable(params string[] locales) => _locales = locales.ToList();

        // ---- 検証用ヘルパ ----

        public int IdOf(string key) => Find(key)?.Id ?? -1;
        public bool IsDeprecated(string key) => Find(key)?.Deprecated ?? false;
        public (string Reason, string PreviousSource)? FuzzyOf(string key) => Find(key)?.Fuzzy;

        public IReadOnlyList<(string PreviousSource, string Locale, string Value)> ArchivedOf(string key)
        {
            var entry = Find(key);
            return entry != null
                ? entry.Archived
                : new List<(string PreviousSource, string Locale, string Value)>();
        }

        public IReadOnlyList<TextSourceRef> SourcesOf(string key)
        {
            var entry = Find(key);
            return entry != null ? entry.Sources : new List<TextSourceRef>();
        }

        // ---- ITextTableEditor ----

        public IReadOnlyList<string> Keys => _entries.Select(e => e.Key).ToList();
        public IReadOnlyList<string> LocaleCodes => _locales;

        public bool ContainsKey(string key) => Find(key) != null;

        public void AddKey(string key)
        {
            if (Find(key) != null) throw new InvalidOperationException($"既存キーの重複追加: {key}");
            _entries.Add(new Entry { Id = _nextId++, Key = key });
        }

        public void RenameKey(string oldKey, string newKey)
        {
            var entry = Find(oldKey) ?? throw new InvalidOperationException($"未登録キーのリネーム: {oldKey}");
            if (Find(newKey) != null) throw new InvalidOperationException($"リネーム先が既存: {newKey}");
            entry.Key = newKey;   // Id は据え置き = 安定 ID を保つ
        }

        public string? GetValue(string key, string localeCode)
            => Find(key) is { } e && e.Values.TryGetValue(localeCode, out var v) ? v : null;

        public void SetValue(string key, string localeCode, string value)
        {
            var entry = Find(key) ?? throw new InvalidOperationException($"未登録キーへの値設定: {key}");
            entry.Values[localeCode] = value;
        }

        public void RemoveValue(string key, string localeCode) => Find(key)?.Values.Remove(localeCode);

        public IReadOnlyList<TextSourceRef> GetSources(string key) => SourcesOf(key);

        public void ClearSources(string key) => Find(key)?.Sources.Clear();

        public void AddSource(string key, string sourceFile, int occurrence)
            => Find(key)?.Sources.Add(new TextSourceRef(sourceFile, occurrence));

        public void SetFuzzy(string key, string reason, string previousSource)
        {
            var entry = Find(key);
            if (entry != null) entry.Fuzzy = (reason, previousSource);
        }

        public void ClearFuzzy(string key)
        {
            var entry = Find(key);
            if (entry != null) entry.Fuzzy = null;
        }

        public void SetDeprecated(string key, bool deprecated)
        {
            var entry = Find(key);
            if (entry != null) entry.Deprecated = deprecated;
        }

        public void AddArchivedTranslation(string key, string previousSource, string localeCode, string value)
            => Find(key)?.Archived.Add((previousSource, localeCode, value));

        public void Save() => SaveCount++;

        private Entry? Find(string key) => _entries.FirstOrDefault(e => e.Key == key);
    }
}
