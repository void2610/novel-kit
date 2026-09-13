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
        /// <param name="lifetime">
        /// 生成単位。シーンごとに独立させたい game は、親スコープで一度登録して <see cref="Lifetime.Scoped"/> を指定する。
        /// <see cref="Lifetime.Transient"/> は未対応
        /// </param>
        public static void RegisterNovelKit(this IContainerBuilder builder, string scenarioRoot = "Scenarios/",
            string rubyResourcePath = RubyDictionary.DefaultKey, Lifetime lifetime = Lifetime.Singleton)
        {
            builder.RegisterNovelKitCore(lifetime);

            // 参考 Resources ローダ（シナリオ / 同梱 preamble の .mrb を Resources から読む）
            var loader = new ResourcesTextAssetLoader();
            builder.Register<IScenarioSource>(_ => new ScenarioSource(loader, scenarioRoot), lifetime);
            builder.Register<IPreambleSource>(_ => new PreambleSource(loader), lifetime);

            builder.Register<IRubyDictionary>(_ =>
            {
                // Configure は PlayerLoop 停止中に走るため、async UniTask (LoadFromAsync) の完了は待てない。
                // Resources ローダーの同期 API から直接読み込む
                var ruby = new RubyDictionary();
                var rubyText = loader.LoadText(rubyResourcePath);
                if (rubyText != null) ruby.Load(rubyText);
                return ruby;
            }, lifetime);

            // スプライトも Resources から。root なしなのでキーは Resources 相対パスそのもの
            // (プレフィックスを付けたい / Addressables にしたい game は ISpriteLoader を後勝ち登録する)
            builder.Register<ISpriteLoader>(_ => new ResourcesSpriteLoader(), lifetime);

            // dev ビルドで未供給コマンドを一度だけ警告する no-op ファセット（コアの silent 既定を上書き）
            builder.Register<IPortraitChannel, WarningPortraitChannel>(lifetime);
            builder.Register<IBackgroundChannel, WarningBackgroundChannel>(lifetime);
            builder.Register<IStillChannel, WarningStillChannel>(lifetime);
            builder.Register<IAudioChannel, WarningAudioChannel>(lifetime);
        }

        /// <summary>
        /// コマンド語彙からの糖衣 preamble 自動生成 (command-sugar-generation ADR) を有効化する。
        /// エディタでは DI ビルド時に語彙をキャプチャして Assets/Resources/Novel/CommandSugars.rb を生成し、
        /// ランタイムではその生成物を IPreambleSource として読む。新しいコマンドの糖衣が効くのは生成後の次の再生から。
        /// RegisterNovelKit の後・game 自前の preamble 登録の前に呼ぶこと (preamble は登録順に評価され、
        /// 手書き糖衣が生成物を後勝ちで上書きできるようにするため)。
        /// </summary>
        public static void RegisterNovelCommandSugars(this IContainerBuilder builder, Lifetime lifetime = Lifetime.Singleton)
        {
            builder.Register<IPreambleSource>(_ => new PreambleSource(new ResourcesTextAssetLoader(), NovelCommandSugars.ResourceKey), lifetime);
#if UNITY_EDITOR
            builder.RegisterBuildCallback(container =>
            {
                try
                {
                    var commands = new System.Collections.Generic.List<CommandKeyInfo>();
                    foreach (var module in container.Resolve<System.Collections.Generic.IEnumerable<INovelCommandModule>>())
                    {
                        var recorder = new RecordingVocabulary(module.GetType().Name);
                        module.RegisterVocabulary(recorder);
                        commands.AddRange(recorder.Commands);
                    }
                    NovelCommandSugars.Publish(commands);
                }
                catch (System.Exception e)
                {
                    UnityEngine.Debug.LogWarning($"[Novel] 糖衣生成用の語彙キャプチャに失敗: {e}");
                }
            });
#endif
        }
    }
}
