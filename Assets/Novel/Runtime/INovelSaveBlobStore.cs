#nullable enable
using System.Threading;
using Cysharp.Threading.Tasks;

namespace Novel.Runtime
{
    // JsonSaveStore(novel-kit 内部完結・serde 隠蔽モード)専用の薄い seam。「JSON 文字列をどこへ永続化するか」
    // だけを担う。JSON 直列化は novel-kit(NovelSaveSerializer)が内部で行い、書き込み先(PlayerPrefs / File /
    // クラウド等)だけを game が本 interface で差し込む。Unity 実装の一例は Novel.View の PlayerPrefsSaveBlobStore。
    //
    // 自前 JSON セーブ機構を持つプロジェクトは本 interface ではなく、自前 ISaveStore + NovelSaveSerializer /
    // NovelSaveData を使う(セーブデータを自分の save に畳み込む)のが標準。
    public interface INovelSaveBlobStore
    {
        UniTask WriteAsync(string json, CancellationToken ct);

        // セーブ未作成なら null を返す(JsonSaveStore は null を空 snapshot として扱う)。
        UniTask<string?> ReadAsync(CancellationToken ct);
    }
}
