#nullable enable
using System.Collections.Generic;
using MRubyCS;

namespace Novel.Runtime
{
    // VitalRouter の共有変数テーブルは self ごとに別インスタンスになり C# と Fiber で実体が分かれるため、self 非依存な定数のハッシュを使う
    internal sealed class MRubyStateStore : IStateStore
    {
        // Preamble の `state` が返す定数名
        public const string ConstName = "NOVEL_STATE";

        private readonly MRubyState _state;
        private readonly RHash _table;
        private readonly HashSet<string> _keys = new();
        private readonly HashSet<string> _read = new();

        public MRubyStateStore(MRubyState state)
        {
            _state = state;
            _table = state.NewHash();
            state.DefineConst(state.Intern(ConstName), MRubyValue.From(_table));
        }

        public int Get(string key)
            => _table.TryGetValue(KeyOf(key), out var value) && value.IsInteger ? (int)value.IntegerValue : 0;

        public bool Has(string key) => _table.TryGetValue(KeyOf(key), out _);

        public bool IsRead(string textId) => _read.Contains(textId);
        public void MarkRead(string textId) => _read.Add(textId);

        public void Set(string key, int value)
        {
            _table[KeyOf(key)] = MRubyValue.From(value);
            _keys.Add(key);
        }

        public void Unset(string key)
        {
            _table.TryDelete(KeyOf(key), out _);
            _keys.Remove(key);
        }

        // セーブ境界（PlayAsync の狭間）でのスナップショット採取/復元。
        // `__` 始まりは一時スクラッチ（choose の自動採番キー等）として永続から除外する（state-model: 永続/一時の境界）。
        // 跨シナリオで残したい選択結果は choose(..., key: :explicit) で `__` 以外の安定キーに書く。
        public NovelStateSnapshot Capture()
        {
            var values = new Dictionary<string, int>(_keys.Count);
            foreach (var k in _keys)
            {
                if (k.StartsWith("__", System.StringComparison.Ordinal)) continue;
                values[k] = Get(k);
            }
            return new NovelStateSnapshot(values, new List<string>(_read));
        }

        public void Restore(NovelStateSnapshot snapshot)
        {
            foreach (var kv in snapshot.Values) Set(kv.Key, kv.Value);
            foreach (var id in snapshot.ReadTextIds) _read.Add(id);
        }

        private MRubyValue KeyOf(string key) => MRubyValue.From(_state.Intern(key));
    }
}
