#nullable enable
using System.Threading;
using Cysharp.Threading.Tasks;

namespace Novel.Runtime
{
    /// <summary>
    /// 論理キーからテキストアセットの中身を読むロード戦略の抽象。
    /// Resources / Addressables 等の Unity ロード手段をここで差し替え、
    /// <c>IScenarioSource</c> / <c>IPreambleSource</c> / ルビ辞書などの供給源はキー解決だけに専念する。
    /// アセット参照ではなく抽出済みの中身を返す契約なので、実装側はロード完了後に即ハンドルを解放してよい。
    /// </summary>
    public interface ITextAssetLoader
    {
        /// <summary>
        /// キーが指すアセット (サブアセット込み) から、名前が <paramref name="subAssetSuffix"/> で終わる
        /// 最初のテキストアセットのバイト列を返す (無ければ null)。
        /// ScriptedImporter が生やす <c>.mrb</c> バイトコードのようなサブアセット取り出しに使う。
        /// </summary>
        UniTask<byte[]?> LoadBytesAsync(string key, string subAssetSuffix, CancellationToken ct);

        /// <summary>キーが指すテキストアセット本体の文字列を返す (無ければ null)。</summary>
        UniTask<string?> LoadTextAsync(string key, CancellationToken ct);
    }
}
