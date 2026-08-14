#nullable enable
using System.Collections.Generic;
using Novel.Runtime;
using UnityEngine;

namespace Novel.View
{
    // ICharacterCatalog の ScriptableObject 実装。インスペクタで id→表示名/立ち絵 を編集する
    // (slot 位置は IPortraitDirector の stage 宣言で決まるため、 旧 side フィールドは撤去)
    [CreateAssetMenu(fileName = "CharacterCatalog", menuName = "Novel/Character Catalog")]
    public sealed class ScriptableCharacterCatalog : ScriptableObject, ICharacterCatalog
    {
        [System.Serializable]
        public struct Entry
        {
            public string speakerId;
            public string displayName;
            public string defaultPortraitKey;
        }

        [SerializeField] private List<Entry> entries = new();

        private Dictionary<string, CharacterEntry>? _map;

        public bool TryGet(string speakerId, out CharacterEntry entry)
        {
            _map ??= Build();
            return _map.TryGetValue(speakerId, out entry);
        }

        // Build() の辞書を経由し、speakerId 重複時も TryGet と同じ解決結果 (後勝ち) を返す
        public IEnumerable<CharacterKeyInfo> EnumerateEntries()
        {
            _map ??= Build();
            foreach (var (id, entry) in _map)
                yield return new CharacterKeyInfo(id, entry.DisplayName, entry.DefaultPortraitKey);
        }

#if UNITY_EDITOR
        // ドメインリロードを無効にしていると同じインスタンスが生き続けるため、 編集を捨てないよう作り直させる
        private void OnValidate() => _map = null;
#endif

        private Dictionary<string, CharacterEntry> Build()
        {
            var map = new Dictionary<string, CharacterEntry>(entries.Count);
            foreach (var e in entries)
            {
                if (string.IsNullOrEmpty(e.speakerId)) continue;
                map[e.speakerId] = new CharacterEntry(
                    string.IsNullOrEmpty(e.displayName) ? e.speakerId : e.displayName,
                    string.IsNullOrEmpty(e.defaultPortraitKey) ? null : e.defaultPortraitKey);
            }
            return map;
        }
    }
}
