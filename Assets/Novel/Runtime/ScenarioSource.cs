#nullable enable
using System.Threading;
using Cysharp.Threading.Tasks;

namespace Novel.Runtime
{
    /// <summary>
    /// RubyScriptedImporter 生成の .mrb バイトコード (サブアセット) を読む <see cref="IScenarioSource"/>。
    /// ロード手段は <see cref="ITextAssetLoader"/> で明示する (Resources なら <c>ResourcesTextAssetLoader</c>)。
    /// </summary>
    public sealed class ScenarioSource : IScenarioSource
    {
        private readonly ITextAssetLoader _loader;
        private readonly string _root;

        public ScenarioSource(ITextAssetLoader loader, string root = "Scenarios/")
        {
            _loader = loader;
            _root = root;
        }

        public UniTask<byte[]?> LoadBytecodeAsync(string scenarioKey, CancellationToken ct)
        {
            // 空キーはロード層に渡さない (Resources.LoadAll が root 配下を総なめして意図しない .mrb を返す事故を防ぐ)
            if (string.IsNullOrEmpty(scenarioKey)) return UniTask.FromResult<byte[]?>(null);
            return _loader.LoadBytesAsync(_root + scenarioKey, ".mrb", ct);
        }
    }
}
