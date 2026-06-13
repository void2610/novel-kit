#nullable enable
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;

namespace Novel.Runtime
{
    // ハンドラが依存する最重要境界。具体 UI でなくこの抽象のみに依存する
    public interface INovelView
    {
        // await が Ruby Fiber をサスペンドし「表示 → 待ち → 次」の線形進行が成立する
        UniTask ShowMessageAsync(NovelLine line, CancellationToken ct);

        // 選択された index を返す
        UniTask<int> ShowChoicesAsync(IReadOnlyList<string> options, CancellationToken ct);
    }
}
