#nullable enable
using System.Collections.Generic;

namespace Novel.Runtime
{
    // 永続対象の状態(フラグ/変数 + 既読 + 再実行で戻せない盤面)のスナップショット。runner の CaptureState/RestoreState で授受する。
    // 直列化は NovelSaveData(クラス)/ NovelSaveSerializer(文字列)。実際の保存は game のセーブ機構の責務。
    public readonly struct NovelStateSnapshot
    {
        public IReadOnlyDictionary<string, int> Values { get; }
        public IReadOnlyCollection<string> ReadTextIds { get; }

        // 実表示中の背景キー。ノベルパートを抜けた先では bg が走らないため、再実行では戻せない
        public string BackgroundKey => _backgroundKey ?? "";
        private readonly string? _backgroundKey;

        public NovelStateSnapshot(IReadOnlyDictionary<string, int> values, IReadOnlyCollection<string> readTextIds,
            string backgroundKey = "")
        {
            Values = values;
            ReadTextIds = readTextIds;
            _backgroundKey = backgroundKey;
        }
    }
}
