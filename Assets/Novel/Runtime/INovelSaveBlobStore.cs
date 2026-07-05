#nullable enable
using System.Threading;
using Cysharp.Threading.Tasks;

namespace Novel.Runtime
{
    // 「JSON 文字列をどこへ永続化するか」だけを担う薄い seam。JSON 直列化は novel-kit(NovelSaveSerializer)が
    // 所有し、実際の書き込み先(PlayerPrefs / File / クラウド等)は game が本 interface で差し込む。
    // Unity 実装の一例は Novel.View の PlayerPrefsSaveBlobStore。
    public interface INovelSaveBlobStore
    {
        UniTask WriteAsync(string json, CancellationToken ct);

        // セーブ未作成なら null を返す(JsonSaveStore は null を空 snapshot として扱う)。
        UniTask<string?> ReadAsync(CancellationToken ct);
    }
}
