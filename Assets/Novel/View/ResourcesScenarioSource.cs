#nullable enable
using System.Threading;
using Cysharp.Threading.Tasks;
using Novel.Runtime;
using UnityEngine;

namespace Novel.View
{
    // Resources 配下から RubyScriptedImporter 生成の .mrb バイトコード（サブアセット TextAsset）を読む既定実装
    public sealed class ResourcesScenarioSource : IScenarioSource
    {
        private readonly string _root;

        public ResourcesScenarioSource(string root = "Scenarios/") => _root = root;

        public UniTask<byte[]?> LoadBytecodeAsync(string scenarioKey, CancellationToken ct)
        {
            // 空キーで Resources.LoadAll("Scenarios/") が root 配下を総なめし、意図しない .mrb を返す事故を防ぐ
            if (string.IsNullOrEmpty(scenarioKey)) return UniTask.FromResult<byte[]?>(null);
            foreach (var a in Resources.LoadAll<TextAsset>(_root + scenarioKey))
                if (a.name.EndsWith(".mrb", System.StringComparison.Ordinal))
                    return UniTask.FromResult<byte[]?>(a.bytes);
            return UniTask.FromResult<byte[]?>(null);
        }
    }
}
