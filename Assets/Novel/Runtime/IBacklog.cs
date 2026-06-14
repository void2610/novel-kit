#nullable enable
using System.Collections.Generic;

namespace Novel.Runtime
{
    // バックログ 1 行（話者・本文）。本文は rich text を保持する（キーワード link 等を含めたまま再表示・収集できるように）。
    public readonly struct BacklogEntry
    {
        public string Speaker { get; }
        public string Text { get; }

        public BacklogEntry(string speaker, string text)
        {
            Speaker = speaker;
            Text = text;
        }
    }

    // 表示済みセリフの履歴。表示のたびに handler が Add する。閲覧 UI と Clear 契機（リトライ/ロード/章移動）は game 所有。
    public interface IBacklog
    {
        IReadOnlyList<BacklogEntry> Entries { get; }
        int Count { get; }
        void Add(string speaker, string text);
        void Clear();
    }
}
