#nullable enable
using Novel.Assets;
using Novel.Runtime;
using System;
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
        /// 生成単位。シーンごとに独立させたい game は、親スコープで一度登録して <see cref="Lifetime.Scoped"/> を指定する。
        /// <see cref="Lifetime.Transient"/> は未対応
        /// </param>
        public static void RegisterNovelKitCore(this IContainerBuilder builder, Lifetime lifetime = Lifetime.Singleton)
        {
            // Transient は注入点ごとに Router と runner (と MRubyState) が分裂し、進行と CaptureState が無言で食い違う
            if (lifetime == Lifetime.Transient)
                throw new ArgumentOutOfRangeException(nameof(lifetime), "Transient は未対応。Singleton か Scoped を指定する");

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
            // エラーは無音にしない (error-handling ADR)。明示的に黙らせたい game は NullErrorHandler を後勝ち登録する
            builder.Register<INovelErrorHandler, DebugNovelErrorHandler>(lifetime);
            // ルビ辞書の no-op 既定 (本文をそのまま返す)。Resources ベース実装は View ヘルパが上書きする
            builder.Register<IRubyDictionary, NullRubyDictionary>(lifetime);
            // テキスト変数 %{key} の game 固有値供給の no-op 既定 (常に IStateStore の変数値へフォールバック)。
            // 主人公名などを差し込む game は ITextVariableProvider を後勝ち登録する
            builder.Register<ITextVariableProvider, NullTextVariableProvider>(lifetime);
            // RingBufferBacklog(int maxLines=200) の既定引数を VContainer は解決できない (int 未登録で Build が落ちる)。
            // ファクトリ登録で既定容量を使う (容量を変えたい game は後勝ちで登録すればよい)。
            builder.Register<IBacklog>(_ => new RingBufferBacklog(), lifetime);

            // 早送り状態・再生中キーを独自コマンドモジュールと共有する (runner と同寿命)
            builder.Register<NovelPlaybackProgress>(lifetime);
            builder.Register<INovelScenarioRunner, NovelScenarioRunner>(lifetime);

#if UNITY_EDITOR
            // 実際に配線されたチャンネル (後勝ち差し替え込み) から目録を吸い上げ、エディタのプロジェクトリファレンスへ渡す (project-reference ADR)。
            // Build 時点で解決すると、非同期生成の View に依存する Singleton が生成前の例外を抱えたまま固定されるため、初回再生時まで待つ
            builder.RegisterBuildCallback(container => NovelProjectCapture.DeferUntilPlayback(() => CaptureProject(container)));
#endif
        }

#if UNITY_EDITOR
        /// <summary>
        /// 種別ごとに独立して目録を取り、取れた分だけをスナップショットとして渡す (空の種別はエディタ側のマージで以前の値が残る)。
        /// 取れなかった種別は警告する。失敗しても再生は妨げない
        /// </summary>
        private static void CaptureProject(IObjectResolver container)
        {
            var failures = new System.Collections.Generic.List<string>();

            void Try(string label, Action capture)
            {
                try
                {
                    capture();
                }
                catch (Exception e)
                {
                    failures.Add($"{label}: {e.Message}");
                }
            }

            var audioKeys = new System.Collections.Generic.List<AudioKeyInfo>();
            var audioType = "";
            Try(nameof(IAudioChannel), () =>
            {
                var audio = container.Resolve<IAudioChannel>();
                audioKeys = new System.Collections.Generic.List<AudioKeyInfo>(audio.EnumerateKeys());
                audioType = audio.GetType().Name;
            });

            var layouts = new System.Collections.Generic.List<StageLayoutInfo>();
            var portraitType = "";
            Try(nameof(IPortraitChannel), () =>
            {
                var portrait = container.Resolve<IPortraitChannel>();
                layouts = new System.Collections.Generic.List<StageLayoutInfo>(portrait.EnumerateLayouts());
                portraitType = portrait.GetType().Name;
            });

            // ICharacterCatalog は game 側登録の契約だが、未登録構成でもキャプチャを落とさない
            var characters = new System.Collections.Generic.List<CharacterKeyInfo>();
            var catalogType = "";
            Try(nameof(ICharacterCatalog), () =>
            {
                if (!container.TryResolve<ICharacterCatalog>(out var catalog)) return;
                characters = new System.Collections.Generic.List<CharacterKeyInfo>(catalog.EnumerateEntries());
                catalogType = catalog.GetType().Name;
            });

            var spriteType = "";
            string? spriteKeyPrefix = null;
            Try(nameof(ISpriteLoader), () =>
            {
                var sprites = container.Resolve<ISpriteLoader>();
                spriteType = sprites.GetType().Name;
                // 名乗らないローダは「プレフィックス不明」として null のまま渡す (空文字の確定と区別する)
                spriteKeyPrefix = (sprites as ISpriteKeyPrefix)?.KeyPrefix;
            });

            var worldEffectKeys = new System.Collections.Generic.List<WorldEffectKeyInfo>();
            var worldEffectType = "";
            Try(nameof(IWorldEffectSink), () =>
            {
                var worldEffects = container.Resolve<IWorldEffectSink>();
                worldEffectKeys = new System.Collections.Generic.List<WorldEffectKeyInfo>(worldEffects.EnumerateKeys());
                worldEffectType = worldEffects.GetType().Name;
            });

            // 語彙は記録用 vocabulary で読む (MRubyState を作らない。RegisterVocabulary は登録以外の副作用を持たない契約)
            var commands = new System.Collections.Generic.List<CommandKeyInfo>();
            Try(nameof(INovelCommandModule), () =>
            {
                var recorded = new System.Collections.Generic.List<CommandKeyInfo>();
                foreach (var module in container.Resolve<System.Collections.Generic.IEnumerable<INovelCommandModule>>())
                {
                    var recorder = new RecordingVocabulary(module.GetType().Name);
                    module.RegisterVocabulary(recorder);
                    recorded.AddRange(recorder.Commands);
                }
                commands = recorded;
            });

            NovelProjectCapture.Publish(new NovelProjectCapture.Snapshot(
                audioKeys, layouts, characters, audioType, portraitType, catalogType, DateTime.Now,
                spriteType, spriteKeyPrefix, commands,
                worldEffectKeys: worldEffectKeys, worldEffectSinkType: worldEffectType));

            if (failures.Count > 0)
                UnityEngine.Debug.LogWarning($"[Novel] プロジェクトリファレンスのキャプチャに失敗: {string.Join(" / ", failures)}");
        }
#endif

        // game 独自コマンドモジュール（[Routes] + INovelCommandModule）を登録する。runner が
        // IEnumerable<INovelCommandModule> として集約注入し、語彙束縛とハンドラ写像を行う。
        // 糖衣の .rb は別途 IPreambleSource として追加登録する（RegisterNovelKit() の後勝ち登録）。
        // コアを Scoped で登録した場合、状態を持つモジュールを既定の Singleton のままにすると runner 間で共有される。
        public static void RegisterNovelCommand<TModule>(this IContainerBuilder builder, Lifetime lifetime = Lifetime.Singleton)
            where TModule : INovelCommandModule
        {
            builder.Register<TModule>(lifetime).As<INovelCommandModule>();
        }
    }
}
