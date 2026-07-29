#nullable enable
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

            // ルビ辞書もローダー抽象を通す (Resources ローダーは同期完了するため GetResult で安全に取り出せる)
            var ruby = new RubyDictionary();
            ruby.LoadFromAsync(loader, rubyResourcePath, System.Threading.CancellationToken.None).GetAwaiter().GetResult();
            builder.RegisterInstance<IRubyDictionary>(ruby);

            // dev ビルドで未供給コマンドを一度だけ警告する no-op ファセット（コアの silent 既定を上書き）
            builder.Register<IPortraitView, WarningPortraitView>(Lifetime.Singleton);
            builder.Register<IBackgroundView, WarningBackgroundView>(Lifetime.Singleton);
            builder.Register<IAudioChannel, WarningAudioChannel>(Lifetime.Singleton);
            // エラーは無音にしない（シナリオ名 + Ruby backtrace をログ。コアの NullErrorHandler を上書き）
            builder.Register<INovelErrorHandler, DebugNovelErrorHandler>(Lifetime.Singleton);
        }
    }
}
