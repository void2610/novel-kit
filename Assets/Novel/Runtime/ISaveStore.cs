#nullable enable
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;

namespace Novel.Runtime
{
    // 永続対象は IStateStore の内容のみ。シリアライズ形式は game が決める
    public readonly struct NovelStateSnapshot
    {
        public IReadOnlyDictionary<string, int> Values { get; }
        public IReadOnlyCollection<string> ReadTextIds { get; }

        public NovelStateSnapshot(IReadOnlyDictionary<string, int> values, IReadOnlyCollection<string> readTextIds)
        {
            Values = values;
            ReadTextIds = readTextIds;
        }
    }

    // セーブ境界は PlayAsync の狭間（シナリオ途中保存は v1 対象外）
    public interface ISaveStore
    {
        UniTask SaveAsync(NovelStateSnapshot snapshot, CancellationToken ct);
        UniTask<NovelStateSnapshot> LoadAsync(CancellationToken ct);
    }
}
