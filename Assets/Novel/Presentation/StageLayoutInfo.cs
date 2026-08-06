#nullable enable
using System.Collections.Generic;

namespace Novel.Assets
{
    /// <summary>
    /// 構図 (レイアウト) の目録エントリ (project-reference ADR)。
    /// <see cref="IPortraitChannel.EnumerateLayouts"/> が返し、エディタのプロジェクトリファレンスが一覧表示する。
    /// </summary>
    public readonly struct StageLayoutInfo
    {
        public string Id { get; }
        public int SlotCount { get; }

        /// <summary>ライター向けメモ (任意)。</summary>
        public string? Note { get; }

        public StageLayoutInfo(string id, int slotCount, string? note = null)
        {
            Id = id;
            SlotCount = slotCount;
            Note = note;
        }

        /// <summary>標準 5 構図 (1〜5 人)。<see cref="IPortraitChannel.EnumerateLayouts"/> の既定値。</summary>
        public static IReadOnlyList<StageLayoutInfo> Defaults { get; } = new[]
        {
            new StageLayoutInfo("single", 1),
            new StageLayoutInfo("pair", 2),
            new StageLayoutInfo("trio", 3),
            new StageLayoutInfo("quad", 4),
            new StageLayoutInfo("penta", 5),
        };
    }
}
