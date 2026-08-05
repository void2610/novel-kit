#nullable enable
using Novel.Assets;
using Novel.Runtime;
using VContainer;
using VitalRouter;

namespace Novel.Integration
{
    // novel-kit の「コア」（純 C# / Novel.Runtime のみ）を VContainer に登録するヘルパ。View/Resources には依存しない。
    // game が別途登録するもの: INovelView / ICharacterCatalog / IScenarioSource / IPreambleSource（＝シナリオと
    // preamble のローダ。Resources を使うなら参考実装が Novel.View にある）。
    // 参考 TMP View・Resources ローダ・dev 警告/ログ既定込みで箱出しに使いたい場合は、
    // Novel.View.VContainer の RegisterNovelKit() を使う（こちらは本 Core を内部で呼ぶ）。
    public static class NovelContainerExtensions
    {
        /// <param name="lifetime">
        /// 生成単位。シーンごとに独立させたい game は、親スコープで一度登録して <see cref="Lifetime.Scoped"/> を指定する
        /// </param>
        public static void RegisterNovelKitCore(this IContainerBuilder builder, Lifetime lifetime = Lifetime.Singleton)
        {
            builder.Register(_ => new Router(), lifetime);

            builder.Register<ITextResolver, IdentityTextResolver>(lifetime);
            builder.Register<INovelPlaybackSettings, DefaultNovelPlaybackSettings>(lifetime);

            // 省略可能ファセット/サービスの no-op 既定（silent）。dev 警告版/ログ版は View ヘルパが上書きする
            builder.Register<IPortraitChannel, NullPortraitChannel>(lifetime);
            // IPortraitDirector の既定は IPortraitChannel を内部で参照する DefaultPortraitDirector。
            // game 側が IPortraitChannel を差し替えれば Director も自動的に差し替え後の実装を使う。
            builder.Register<IPortraitDirector, DefaultPortraitDirector>(lifetime);
            builder.Register<IBackgroundChannel, NullBackgroundChannel>(lifetime);
            builder.Register<IStillChannel, NullStillChannel>(lifetime);
            builder.Register<ICenterImageChannel, NullCenterImageChannel>(lifetime);
            builder.Register<IAudioChannel, NullAudioChannel>(lifetime);
            builder.Register<IWorldEffectSink, NullWorldEffectSink>(lifetime);
            // スプライト解決の no-op 既定 (常に null)。Resources/Addressables 実装は game か View ヘルパが上書きする
            builder.Register<ISpriteLoader, NullSpriteLoader>(lifetime);
            builder.Register<INovelErrorHandler, NullErrorHandler>(lifetime);
            // ルビ辞書の no-op 既定 (本文をそのまま返す)。Resources ベース実装は View ヘルパが上書きする
            builder.Register<IRubyDictionary, NullRubyDictionary>(lifetime);
            // RingBufferBacklog(int maxLines=200) の既定引数を VContainer は解決できない (int 未登録で Build が落ちる)。
            // ファクトリ登録で既定容量を使う (容量を変えたい game は後勝ちで登録すればよい)。
            builder.Register<IBacklog>(_ => new RingBufferBacklog(), lifetime);

            builder.Register<INovelScenarioRunner, NovelScenarioRunner>(lifetime);
        }

        // game 独自コマンドモジュール（[Routes] + INovelCommandModule）を登録する。runner が
        // IEnumerable<INovelCommandModule> として集約注入し、語彙束縛とハンドラ写像を行う。
        // 糖衣の .rb は別途 IPreambleSource として追加登録する（RegisterNovelKit() の後勝ち登録）。
        public static void RegisterNovelCommand<TModule>(this IContainerBuilder builder, Lifetime lifetime = Lifetime.Singleton)
            where TModule : INovelCommandModule
        {
            builder.Register<TModule>(lifetime).As<INovelCommandModule>();
        }
    }
}
