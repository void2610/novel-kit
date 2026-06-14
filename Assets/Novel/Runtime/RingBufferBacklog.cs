#nullable enable
using System.Collections.Generic;

namespace Novel.Runtime
{
    // 既定の IBacklog。上限を超えたら最古から捨てるリングバッファ（既定 200 行）。純 C#・UI 非依存。
    public sealed class RingBufferBacklog : IBacklog
    {
        public const int DefaultMaxLines = 200;

        private readonly List<BacklogEntry> _entries = new();
        private readonly int _maxLines;

        public RingBufferBacklog(int maxLines = DefaultMaxLines)
        {
            _maxLines = maxLines < 1 ? 1 : maxLines;
        }

        public IReadOnlyList<BacklogEntry> Entries => _entries;
        public int Count => _entries.Count;

        public void Add(string speaker, string text)
        {
            _entries.Add(new BacklogEntry(speaker ?? "", text ?? ""));
            if (_entries.Count > _maxLines) _entries.RemoveRange(0, _entries.Count - _maxLines);
        }

        public void Clear() => _entries.Clear();
    }
}
