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
        public static void RegisterNovelKit(this IContainerBuilder builder, string scenarioRoot = "Scenarios/")
        {
            builder.RegisterNovelKitCore();

            // 参考 Resources ローダ（シナリオ / 同梱 preamble の .mrb を Resources から読む）
            builder.RegisterInstance<IScenarioSource>(new ResourcesScenarioSource(scenarioRoot));
            builder.RegisterInstance<IPreambleSource>(new ResourcesPreambleSource());

            // dev ビルドで未供給コマンドを一度だけ警告する no-op ファセット（コアの silent 既定を上書き）
            builder.Register<IPortraitView, WarningPortraitView>(Lifetime.Singleton);
            builder.Register<IBackgroundView, WarningBackgroundView>(Lifetime.Singleton);
            builder.Register<IAudioChannel, WarningAudioChannel>(Lifetime.Singleton);
            // エラーは無音にしない（シナリオ名 + Ruby backtrace をログ。コアの NullErrorHandler を上書き）
            builder.Register<INovelErrorHandler, DebugNovelErrorHandler>(Lifetime.Singleton);
        }
    }
}
