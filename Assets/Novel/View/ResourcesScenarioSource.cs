#nullable enable
using System.Threading;
using Cysharp.Threading.Tasks;
using Novel.Runtime;

namespace Novel.View
{
    /// <summary>Resources 既定の <see cref="IScenarioSource"/>。実体は <see cref="TextAssetScenarioSource"/> + <see cref="ResourcesTextAssetLoader"/>。</summary>
    public sealed class ResourcesScenarioSource : IScenarioSource
    {
        private readonly TextAssetScenarioSource _inner;

        public ResourcesScenarioSource(string root = "Scenarios/") => _inner = new TextAssetScenarioSource(new ResourcesTextAssetLoader(), root);

        public UniTask<byte[]?> LoadBytecodeAsync(string scenarioKey, CancellationToken ct) => _inner.LoadBytecodeAsync(scenarioKey, ct);
    }
}
