#nullable enable
using Cysharp.Threading.Tasks;
using Novel.Integration;
using Novel.Runtime;
using Novel.View;
using UnityEngine;
using UnityEngine.EventSystems;
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
            builder.RegisterInstance<INovelPlaybackSettings>(new SampleNovelSettings()); // skip 効果を 1 周で見せる
            builder.RegisterEntryPoint<SampleNovelStarter>();           // 起動時に再生
            builder.RegisterBuildCallback(c =>
            {
                // View は VContainer 非依存なので解決済みの設定を手動で渡す
                if (c.Resolve<INovelView>() is NovelMessageView mv)
                    mv.Configure(c.Resolve<INovelPlaybackSettings>());
            });
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
            // UI（Auto/Skip ボタン等）上のクリックは送りに使わない
            var overUI = EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();
            var space = Keyboard.current != null && Keyboard.current.spaceKey.wasPressedThisFrame;
            var click = Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame && !overUI;
            if (space || click) view.Advance();
        }
    }

    // テスト用の再生設定。skip で未読も飛ばし、auto をやや速める
    public sealed class SampleNovelSettings : INovelPlaybackSettings
    {
        public float CharsPerSecond => 30f;
        public float AutoAdvanceDelay => 1.0f;
        public bool SkipUnread => true;
    }
}
