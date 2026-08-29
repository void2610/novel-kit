#nullable enable
using Novel.Cinematic;
using Novel.Runtime;
using Novel.View;
using UnityEngine;
using VContainer;
using Void2610.CinematicEffect;

namespace Novel.Integration
{
    /// <summary>
    /// CinematicEffect をシナリオから使うための一括登録。<c>RegisterNovelKit()</c> / <c>RegisterNovelKitCore()</c> の後に呼ぶ。
    /// これ以降、<c>Resources/Novel/Effects/&lt;key&gt;.asset</c> を置くだけで <c>cinematic :key</c> が使える。
    /// </summary>
    public static class NovelCinematicEffectContainerExtensions
    {
        public static void RegisterNovelCinematicEffects(this IContainerBuilder builder, Lifetime lifetime = Lifetime.Singleton)
        {
            // Director はシーンにあればそれを使い、無ければ生成する (Resolve 時 = シーンロード後に探す)
            builder.Register(_ => FindOrCreateDirector(), lifetime);
            builder.Register<ICinematicSequenceLoader, ResourcesCinematicSequenceLoader>(lifetime);
            builder.RegisterNovelCommand<CinematicCommandModule>(lifetime);
            builder.Register<IPreambleSource>(_ => new PreambleSource(new ResourcesTextAssetLoader(), CinematicCommandModule.PreambleKey), lifetime);
            // 標準 5 種 (shake 等) の既定。独自の world_effect を持つ game は後勝ちで差し替える
            builder.Register<IWorldEffectSink, BuiltinTransitionWorldEffectSink>(lifetime);
        }

        // 各エフェクトは Director の Awake で自己構築されるため、Director 本体さえあれば事前配置は要らない
        private static CinematicEffectDirector FindOrCreateDirector()
        {
            var existing = Object.FindFirstObjectByType<CinematicEffectDirector>(FindObjectsInactive.Include);
            if (existing != null) return existing;
            return new GameObject(nameof(CinematicEffectDirector)).AddComponent<CinematicEffectDirector>();
        }
    }
}
