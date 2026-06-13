using System.Threading;
using Cysharp.Threading.Tasks;
using Novel.Commands;
using VitalRouter;

namespace Novel.Runtime
{
    // ノベル専用 Router にマップされる DI 市民。ハンドラ await で進行が成立する（Fiber サスペンション）
    [Routes]
    public partial class NovelCommandHandler
    {
        // 骨組み: 実際の提示・既読/タグ/状態反映は INovelView 配線後に実装する
        public async UniTask On(SayCommand cmd, CancellationToken ct)
        {
            await UniTask.CompletedTask;
        }
    }
}
