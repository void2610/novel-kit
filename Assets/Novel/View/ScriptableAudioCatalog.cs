#nullable enable
using System.Collections.Generic;
using Novel.Runtime;
using UnityEngine;

namespace Novel.View
{
    /// <summary>
    /// 音キー → AudioClip の参考カタログ (project-reference ADR)。インスペクタで編集し、
    /// キーがそのままシナリオの se/bgm 命令の名前になる。<see cref="NovelAudioPlayer"/> がキー解決に使い、
    /// エディタのプロジェクトリファレンスは <see cref="EnumerateKeys"/> を一覧表示に使う。
    /// </summary>
    [CreateAssetMenu(fileName = "AudioCatalog", menuName = "Novel/Audio Catalog")]
    public sealed class ScriptableAudioCatalog : ScriptableObject
    {
        [System.Serializable]
        public struct Entry
        {
            public string key;
            public AudioKeyKind kind;
            public AudioClip clip;
            [Tooltip("ライター向けメモ (プロジェクトリファレンスに表示)")]
            public string note;
        }

        [SerializeField] private List<Entry> entries = new();

        private Dictionary<(string, AudioKeyKind), AudioClip>? _map;

        public bool TryGet(string key, AudioKeyKind kind, out AudioClip clip)
        {
            _map ??= Build();
            return _map.TryGetValue((key, kind), out clip!);
        }

        /// <summary>目録 (Serialized リストを直接読むだけで、クリップのロード状態に依存しない)。</summary>
        public IEnumerable<AudioKeyInfo> EnumerateKeys()
        {
            foreach (var e in entries)
                if (!string.IsNullOrEmpty(e.key))
                    yield return new AudioKeyInfo(e.key, e.kind, string.IsNullOrEmpty(e.note) ? null : e.note);
        }

        private Dictionary<(string, AudioKeyKind), AudioClip> Build()
        {
            var map = new Dictionary<(string, AudioKeyKind), AudioClip>(entries.Count);
            foreach (var e in entries)
            {
                if (string.IsNullOrEmpty(e.key) || e.clip == null) continue;
                map[(e.key, e.kind)] = e.clip;
            }
            return map;
        }
    }
}
