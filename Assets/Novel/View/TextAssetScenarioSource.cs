#nullable enable
using System.Threading;
using Cysharp.Threading.Tasks;
using Novel.Runtime;

namespace Novel.View
{
    /// <summary>
    /// <see cref="ITextAssetLoader"/> 経由で RubyScriptedImporter 生成の .mrb バイトコード
    /// (サブアセット) を読む <see cref="IScenarioSource"/>。ロード手段はローダー差し替えで
    /// Resources / Addressables 等を選べる。
    /// </summary>
    public sealed class TextAssetScenarioSource : IScenarioSource
    {
        private readonly ITextAssetLoader _loader;
        private readonly string _root;

        public TextAssetScenarioSource(ITextAssetLoader loader, string root = "Scenarios/")
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
