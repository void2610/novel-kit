#nullable enable
using System.Collections.Generic;
using VitalRouter.MRuby;

namespace Novel.Runtime
{
    // MRubyState の共有変数テーブルを実体とする IStateStore。
    // フラグ/変数を共有テーブルへ置くことで Ruby の state[:key] と自動同期する。既読は別途インメモリ。
    // セーブ対象の特定のため Set したキーを追跡する
    internal sealed class MRubyStateStore : IStateStore
    {
        private readonly MRubySharedVariableTable _shared;
        private readonly HashSet<string> _keys = new();
        private readonly HashSet<string> _read = new();

        public MRubyStateStore(MRubySharedVariableTable shared) => _shared = shared;

        public int Get(string key) => _shared.GetOrDefault<int>(key);

        public void Set(string key, int value)
        {
            _shared.Set(key, value);
            _keys.Add(key);
        }

        public void Unset(string key)
        {
            _shared.Remove(key);
            _keys.Remove(key);
        }

        public bool Has(string key) => _shared.HasKey(key);

        public bool IsRead(string textId) => _read.Contains(textId);
        public void MarkRead(string textId) => _read.Add(textId);

        // セーブ境界（PlayAsync の狭間）でのスナップショット採取/復元。
        // `__` 始まりは一時スクラッチ（choose の自動採番キー等）として永続から除外する（state-model: 永続/一時の境界）。
        // 跨シナリオで残したい選択結果は choose(..., key: :explicit) で `__` 以外の安定キーに書く。
        public NovelStateSnapshot Capture()
        {
            var values = new Dictionary<string, int>(_keys.Count);
            foreach (var k in _keys)
            {
                if (k.StartsWith("__")) continue;
                values[k] = _shared.GetOrDefault<int>(k);
            }
            return new NovelStateSnapshot(values, new List<string>(_read));
        }

        public void Restore(NovelStateSnapshot snapshot)
        {
            foreach (var kv in snapshot.Values) Set(kv.Key, kv.Value);
            foreach (var id in snapshot.ReadTextIds) _read.Add(id);
        }
    }
}
