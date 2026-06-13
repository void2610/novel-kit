#nullable enable
using System.Threading;
using Cysharp.Threading.Tasks;
using MRubyCS;

namespace Novel.Runtime
{
    // 論理キー → コンパイル済みバイトコード（Irep）を解決する。
    // .mrb sub-asset は mrubycs-compiler の ScriptedImporter が生成したものを game がロードする
    public interface IScenarioSource
    {
        UniTask<Irep> LoadAsync(string scenarioKey, CancellationToken ct);
    }
}
