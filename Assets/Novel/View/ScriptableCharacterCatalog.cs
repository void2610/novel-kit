#nullable enable
using System.Collections.Generic;
using Novel.Runtime;
using UnityEngine;

namespace Novel.View
{
    // ICharacterCatalog の ScriptableObject 実装。インスペクタで id→表示名/立ち絵/side を編集する
    [CreateAssetMenu(fileName = "CharacterCatalog", menuName = "Novel/Character Catalog")]
    public sealed class ScriptableCharacterCatalog : ScriptableObject, ICharacterCatalog
    {
        [System.Serializable]
        public struct Entry
        {
            public string speakerId;
            public string displayName;
            public string defaultPortraitKey;
            public string side;
        }

        [SerializeField] private List<Entry> entries = new();

        private Dictionary<string, CharacterEntry>? _map;

        public bool TryGet(string speakerId, out CharacterEntry entry)
        {
            _map ??= Build();
            return _map.TryGetValue(speakerId, out entry);
        }

        private Dictionary<string, CharacterEntry> Build()
        {
            var map = new Dictionary<string, CharacterEntry>(entries.Count);
            foreach (var e in entries)
            {
                if (string.IsNullOrEmpty(e.speakerId)) continue;
                map[e.speakerId] = new CharacterEntry(
                    string.IsNullOrEmpty(e.displayName) ? e.speakerId : e.displayName,
                    string.IsNullOrEmpty(e.defaultPortraitKey) ? null : e.defaultPortraitKey,
                    string.IsNullOrEmpty(e.side) ? null : e.side);
            }
            return map;
        }
    }
}
