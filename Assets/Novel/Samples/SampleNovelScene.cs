#nullable enable
using Cysharp.Threading.Tasks;
using Novel.Integration;
using Novel.Runtime;
using Novel.View;
using UnityEngine;
using UnityEngine.InputSystem;
using VContainer;
using VContainer.Unity;

namespace Novel.Samples
{
    // novel-kit を最小構成で配線するサンプル。シーンに本コンポーネント + NovelMessageView を置いて使う。
    public sealed class SampleNovelLifetimeScope : LifetimeScope
    {
        [SerializeField] private NovelMessageView view = null!;
        [SerializeField] private ScriptableCharacterCatalog catalog = null!;

        protected override void Configure(IContainerBuilder builder)
        {
            builder.RegisterNovelKit();                                  // 既定実装を一括登録
            builder.RegisterComponent(view).As<INovelView>();           // game の View
            builder.RegisterInstance<ICharacterCatalog>(catalog);       // game のカタログ
            builder.RegisterEntryPoint<SampleNovelStarter>();           // 起動時に再生
        }
    }

    // 起動時に "sample" シナリオを再生するエントリポイント
    public sealed class SampleNovelStarter : IStartable
    {
        private readonly INovelScenarioRunner _runner;

        public SampleNovelStarter(INovelScenarioRunner runner) => _runner = runner;

        public void Start() => _runner.PlayAsync("sample", default).Forget();
    }

    // 送り入力（スペース/クリック）を NovelMessageView.Advance へ橋渡しするサンプル
    public sealed class SampleAdvanceInput : MonoBehaviour
    {
        [SerializeField] private NovelMessageView view = null!;

        private void Update()
        {
            var space = Keyboard.current != null && Keyboard.current.spaceKey.wasPressedThisFrame;
            var click = Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame;
            if (space || click) view.Advance();
        }
    }
}
