#nullable enable
using Novel.Runtime;
using Novel.View;
using VContainer;
using VitalRouter;

namespace Novel.Integration
{
    // novel-kit の既定実装を VContainer に一括登録するヘルパ。
    // INovelView と ICharacterCatalog は game 固有のため、別途 game が登録する前提。
    // 省略可能サービスは no-op 既定で埋める（game が上書き登録すればそちらが優先）。
    public static class NovelContainerExtensions
    {
        public static void RegisterNovelKit(this IContainerBuilder builder, string scenarioRoot = "Scenarios/")
        {
            builder.RegisterInstance(new Router());
            builder.RegisterInstance<IScenarioSource>(new ResourcesScenarioSource(scenarioRoot));
            builder.RegisterInstance<IPreambleSource>(new ResourcesPreambleSource());

            builder.Register<ITextResolver, IdentityTextResolver>(Lifetime.Singleton);
            builder.Register<INovelPlaybackSettings, DefaultNovelPlaybackSettings>(Lifetime.Singleton);

            // 省略可能ファセットの no-op 既定（game が view 実装で上書き可）。
            // dev ビルドでは未供給コマンドを一度だけ警告する（無言ドロップを避ける。本番は黙る）。
            builder.Register<IPortraitView, WarningPortraitView>(Lifetime.Singleton);
            builder.Register<IBackgroundView, WarningBackgroundView>(Lifetime.Singleton);
            builder.Register<IAudioChannel, WarningAudioChannel>(Lifetime.Singleton);
            builder.Register<IWorldEffectSink, NullWorldEffectSink>(Lifetime.Singleton);
            builder.Register<ISaveStore, NullSaveStore>(Lifetime.Singleton);
            // 既定は無音にしない（シナリオ名 + Ruby backtrace をログ）。game は自前実装で上書き可
            builder.Register<INovelErrorHandler, DebugNovelErrorHandler>(Lifetime.Singleton);

            builder.Register<INovelScenarioRunner, NovelScenarioRunner>(Lifetime.Singleton);
        }
    }
}
