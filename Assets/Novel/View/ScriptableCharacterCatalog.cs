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
