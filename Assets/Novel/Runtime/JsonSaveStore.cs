#nullable enable
using System.Threading;
using Cysharp.Threading.Tasks;

namespace Novel.Runtime
{
    // 【標準の統合方法ではない】novel-kit が永続まで内部で完結して面倒を見る用途の ISaveStore 実装。
    // 直列化(NovelSaveSerializer)を内部で行い、ゲームには JSON を一切見せない(serde を隠蔽する)。
    // ゲームは「どこへ書くか」= INovelSaveBlobStore(string の read/write)だけを供給する。
    //
    // 大半のプロジェクトは自前の JSON セーブ機構を持っているので、通常は本クラスを使わず、
    // 自前 ISaveStore を実装して NovelSaveSerializer(文字列)/ NovelSaveData(クラス)でセーブデータを
    // 受け取り、自分のセーブに畳み込む。本クラスは「novel-kit に丸ごと任せたい」小規模ケース向け。
    //
    // 破損/未作成セーブは空 snapshot として復元する(LoadAsync は throw せず新規開始へフォールバック)。
    public sealed class JsonSaveStore : ISaveStore
    {
        private readonly INovelSaveBlobStore _blob;

        public JsonSaveStore(INovelSaveBlobStore blob) => _blob = blob;

        public UniTask SaveAsync(NovelStateSnapshot snapshot, CancellationToken ct)
            => _blob.WriteAsync(NovelSaveSerializer.Serialize(snapshot), ct);

        public async UniTask<NovelStateSnapshot> LoadAsync(CancellationToken ct)
        {
            var json = await _blob.ReadAsync(ct);
            return NovelSaveSerializer.TryDeserialize(json, out var snapshot)
                ? snapshot
                : NovelSaveSerializer.Empty;
        }
    }
}
