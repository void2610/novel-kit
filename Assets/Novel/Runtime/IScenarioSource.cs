#nullable enable
using System.Threading;
using Cysharp.Threading.Tasks;

namespace Novel.Runtime
{
    // 論理キー → コンパイル済み .mrb バイトコードを解決する。
    // バイトコードは mrubycs-compiler の ScriptedImporter 生成物を game がロードする（Resources/Addressables 等）。
    // Irep へのパースは MRubyState 依存のため runner 側が行う。null = ソース無し（何も再生しない）
    public interface IScenarioSource
    {
        UniTask<byte[]?> LoadBytecodeAsync(string scenarioKey, CancellationToken ct);
    }
}
