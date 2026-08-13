#nullable enable
using System.Collections.Generic;
using System.Linq;
using Novel.Editor.Localization;
using UnityEditor;
using UnityEditor.Localization;
using UnityEngine.Localization.Tables;

namespace Novel.Localization.Editor
{
    /// <summary>
    /// <see cref="ITextTableEditor"/> の Unity Localization 実装（第一実装のアダプタ）。
    /// キーの安定 ID は <see cref="SharedTableData"/> の KeyId で、<see cref="RenameKey"/> は
    /// <c>SharedTableData.RenameKey</c> に委譲するため訳とメタデータがそのまま追従する。
    /// 計画立案・適用のロジック本体は Unity 非依存の <see cref="ExtractionPlanner"/> /
    /// <see cref="ExtractionApplier"/> にあり、本クラスは翻訳層に徹する。
    /// </summary>
    public sealed class UnityLocalizationTableEditor : ITextTableEditor
    {
        private readonly StringTableCollection _collection;

        public UnityLocalizationTableEditor(StringTableCollection collection) => _collection = collection;

        private SharedTableData Shared => _collection.SharedData;

        public IReadOnlyList<string> Keys => Shared.Entries.Select(e => e.Key).ToList();

        public IReadOnlyList<string> LocaleCodes =>
            _collection.StringTables.Select(t => t.LocaleIdentifier.Code).ToList();

        public bool ContainsKey(string key) => Shared.Contains(key);

        public void AddKey(string key) => Shared.AddKey(key);

        public void RenameKey(string oldKey, string newKey) => Shared.RenameKey(oldKey, newKey);

        public string? GetValue(string key, string localeCode)
        {
            var entry = Shared.GetEntry(key);
            if (entry == null) return null;
            return TableOf(localeCode)?.GetEntry(entry.Id)?.Value;
        }

        public void SetValue(string key, string localeCode, string value)
            => TableOf(localeCode)?.AddEntry(key, value);

        public void RemoveValue(string key, string localeCode)
        {
            var entry = Shared.GetEntry(key);
            var table = TableOf(localeCode);
            if (entry == null || table == null) return;
            if (table.GetEntry(entry.Id) != null) table.RemoveEntry(entry.Id);
        }

        public IReadOnlyList<TextSourceRef> GetSources(string key)
        {
            var entry = Shared.GetEntry(key);
            if (entry == null) return System.Array.Empty<TextSourceRef>();
            return entry.Metadata.MetadataEntries.OfType<NovelTextSourceMetadata>()
                .Select(m => new TextSourceRef(m.SourceFile, m.Occurrence)).ToList();
        }

        public void ClearSources(string key)
        {
            var entry = Shared.GetEntry(key);
            if (entry == null) return;
            foreach (var meta in entry.Metadata.MetadataEntries.OfType<NovelTextSourceMetadata>().ToList())
                entry.Metadata.RemoveMetadata(meta);
        }

        public void AddSource(string key, string sourceFile, int occurrence)
            => Shared.GetEntry(key)?.Metadata.AddMetadata(
                new NovelTextSourceMetadata { SourceFile = sourceFile, Occurrence = occurrence });

        public void SetFuzzy(string key, string reason, string previousSource)
            => Shared.GetEntry(key)?.Metadata.AddMetadata(
                new NovelFuzzyMetadata { Reason = reason, PreviousSource = previousSource });

        public void ClearFuzzy(string key)
        {
            var entry = Shared.GetEntry(key);
            if (entry == null) return;
            foreach (var meta in entry.Metadata.MetadataEntries.OfType<NovelFuzzyMetadata>().ToList())
                entry.Metadata.RemoveMetadata(meta);
        }

        public void SetDeprecated(string key, bool deprecated)
        {
            var entry = Shared.GetEntry(key);
            if (entry == null) return;
            var existing = entry.Metadata.MetadataEntries.OfType<NovelDeprecatedMetadata>().ToList();
            if (deprecated)
            {
                if (existing.Count == 0) entry.Metadata.AddMetadata(new NovelDeprecatedMetadata());
            }
            else
            {
                foreach (var meta in existing) entry.Metadata.RemoveMetadata(meta);
            }
        }

        public void AddArchivedTranslation(string key, string previousSource, string localeCode, string value)
            => Shared.GetEntry(key)?.Metadata.AddMetadata(new NovelArchivedTranslationMetadata
            {
                PreviousSource = previousSource,
                LocaleCode = localeCode,
                Value = value,
            });

        public void Save()
        {
            // SaveAssets() はプロジェクト全体の保存が走るため、対象アセットに絞って保存する
            EditorUtility.SetDirty(Shared);
            AssetDatabase.SaveAssetIfDirty(Shared);
            foreach (var table in _collection.StringTables)
            {
                EditorUtility.SetDirty(table);
                AssetDatabase.SaveAssetIfDirty(table);
            }
        }

        private StringTable? TableOf(string localeCode)
            => _collection.StringTables.FirstOrDefault(t => t.LocaleIdentifier.Code == localeCode);
    }
}
