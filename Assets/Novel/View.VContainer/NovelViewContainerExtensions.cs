#nullable enable
using Novel.Assets;
using Novel.Runtime;
using Novel.View;
using VContainer;

namespace Novel.Integration
{
    // コア（RegisterNovelKitCore）+ 参考 TMP View 向けの Resources ローダ・dev 警告/ログ既定を一括登録する。
    // 箱出しで動かしたい game 向け。コアだけ欲しい（自前 View / 独自ローダ）場合は Novel.VContainer の
    // RegisterNovelKitCore() を使い、IScenarioSource / IPreambleSource を自前で登録する。
    // INovelView と ICharacterCatalog は game 固有のため、いずれの場合も別途 game が登録する前提。
    public static class NovelViewContainerExtensions
    {
        public static void RegisterNovelKit(this IContainerBuilder builder, string scenarioRoot = "Scenarios/",
            string rubyResourcePath = RubyDictionary.DefaultKey)
        {
            builder.RegisterNovelKitCore();

            // 参考 Resources ローダ（シナリオ / 同梱 preamble の .mrb を Resources から読む）
            var loader = new ResourcesTextAssetLoader();
            builder.RegisterInstance<IScenarioSource>(new ScenarioSource(loader, scenarioRoot));
            builder.RegisterInstance<IPreambleSource>(new PreambleSource(loader));

            // Configure は PlayerLoop 停止中に走るため、async UniTask (LoadFromAsync) の完了は待てない。
            // Resources ローダーの同期 API から直接読み込む
            var ruby = new RubyDictionary();
            var rubyText = loader.LoadText(rubyResourcePath);
            if (rubyText != null) ruby.Load(rubyText);
            builder.RegisterInstance<IRubyDictionary>(ruby);

            // スプライトも Resources から。root なしなのでキーは Resources 相対パスそのもの
            // (プレフィックスを付けたい / Addressables にしたい game は ISpriteLoader を後勝ち登録する)
            builder.RegisterInstance<ISpriteLoader>(new ResourcesSpriteLoader());

            // dev ビルドで未供給コマンドを一度だけ警告する no-op ファセット（コアの silent 既定を上書き）
            builder.Register<IPortraitChannel, WarningPortraitChannel>(Lifetime.Singleton);
            builder.Register<IBackgroundChannel, WarningBackgroundChannel>(Lifetime.Singleton);
            builder.Register<IStillChannel, WarningStillChannel>(Lifetime.Singleton);
            builder.Register<IAudioChannel, WarningAudioChannel>(Lifetime.Singleton);
            // エラーは無音にしない（シナリオ名 + Ruby backtrace をログ。コアの NullErrorHandler を上書き）
            builder.Register<INovelErrorHandler, DebugNovelErrorHandler>(Lifetime.Singleton);
        }
    }
}
