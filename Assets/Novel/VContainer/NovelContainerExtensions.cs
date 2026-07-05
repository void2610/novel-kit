#nullable enable
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
        public static void RegisterNovelKitCore(this IContainerBuilder builder)
        {
            builder.RegisterInstance(new Router());

            builder.Register<ITextResolver, IdentityTextResolver>(Lifetime.Singleton);
            builder.Register<INovelPlaybackSettings, DefaultNovelPlaybackSettings>(Lifetime.Singleton);

            // 省略可能ファセット/サービスの no-op 既定（silent）。dev 警告版/ログ版は View ヘルパが上書きする
            builder.Register<IPortraitView, NullPortraitView>(Lifetime.Singleton);
            // IPortraitDirector の既定は IPortraitView を内部で参照する DefaultPortraitDirector。
            // game 側が IPortraitView を差し替えれば Director も自動的に新 View を使う。
            builder.Register<IPortraitDirector, DefaultPortraitDirector>(Lifetime.Singleton);
            builder.Register<IBackgroundView, NullBackgroundView>(Lifetime.Singleton);
            builder.Register<IAudioChannel, NullAudioChannel>(Lifetime.Singleton);
            builder.Register<IWorldEffectSink, NullWorldEffectSink>(Lifetime.Singleton);
            builder.Register<ISaveStore, NullSaveStore>(Lifetime.Singleton);
            builder.Register<INovelErrorHandler, NullErrorHandler>(Lifetime.Singleton);
            // ルビ辞書の no-op 既定 (本文をそのまま返す)。Resources ベース実装は View ヘルパが上書きする
            builder.Register<IRubyDictionary, NullRubyDictionary>(Lifetime.Singleton);
            // RingBufferBacklog(int maxLines=200) の既定引数を VContainer は解決できない (int 未登録で Build が落ちる)。
            // インスタンス登録で既定容量を使う (容量を変えたい game は後勝ちで RegisterInstance<IBacklog> すればよい)。
            builder.RegisterInstance<IBacklog>(new RingBufferBacklog());

            builder.Register<INovelScenarioRunner, NovelScenarioRunner>(Lifetime.Singleton);
        }

        // 【任意】novel-kit 内部完結モード: JsonSaveStore を ISaveStore として登録し、永続まで novel-kit に
        // 任せる(ゲームは serde に触れない)。永続先の INovelSaveBlobStore だけ型引数で指定する
        // (Unity 実装例は Novel.View の PlayerPrefsSaveBlobStore)。コアの NullSaveStore 既定を後勝ちで上書き。
        //   例: builder.RegisterNovelJsonSave<PlayerPrefsSaveBlobStore>();
        // 自前 JSON セーブ機構を持つプロジェクトはこれを使わず、自前 ISaveStore を Register し、その中で
        // NovelSaveSerializer(文字列)/ NovelSaveData(クラス)を使ってセーブデータを自分の save に畳み込む。
        public static void RegisterNovelJsonSave<TBlob>(this IContainerBuilder builder)
            where TBlob : INovelSaveBlobStore
        {
            builder.Register<INovelSaveBlobStore, TBlob>(Lifetime.Singleton);
            builder.Register<ISaveStore, JsonSaveStore>(Lifetime.Singleton);
        }

        // game 独自コマンドモジュール（[Routes] + INovelCommandModule）を登録する。runner が
        // IEnumerable<INovelCommandModule> として集約注入し、語彙束縛とハンドラ写像を行う。
        // 糖衣の .rb は別途 IPreambleSource として追加登録する（RegisterNovelKit() の後勝ち登録）。
        public static void RegisterNovelCommand<TModule>(this IContainerBuilder builder)
            where TModule : INovelCommandModule
        {
            builder.Register<TModule>(Lifetime.Singleton).As<INovelCommandModule>();
        }
    }
}
