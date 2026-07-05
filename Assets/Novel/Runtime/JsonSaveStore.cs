#nullable enable
using System.Threading;
using Cysharp.Threading.Tasks;

namespace Novel.Runtime
{
    // JSON 形式の ISaveStore 既定実装。直列化は NovelSaveSerializer(ライブラリ所有)、永続先は
    // INovelSaveBlobStore(game 供給)に委譲する。破損/未作成セーブは空 snapshot として復元する
    // (LoadAsync は決して throw せず、新規開始にフォールバックする)。
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
