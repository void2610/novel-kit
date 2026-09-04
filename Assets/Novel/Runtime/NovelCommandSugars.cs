#nullable enable
using System;
using System.Collections.Generic;

namespace Novel.Runtime
{
    /// <summary>
    /// コマンド語彙からの糖衣 preamble 自動生成 (command-sugar-generation ADR) の共有定数と、
    /// DI ビルド時キャプチャのエディタ向け受け口。生成された `.rb` は Resources の
    /// <see cref="ResourceKey"/> に置かれ、`RegisterNovelCommandSugars()` が IPreambleSource として読む。
    /// </summary>
    public static class NovelCommandSugars
    {
        /// <summary>生成される糖衣 preamble の Resources キー (実体は Assets/Resources/Novel/CommandSugars.rb)。</summary>
        public const string ResourceKey = "Novel/CommandSugars";

        /// <summary>
        /// 組込語彙のコマンド名。糖衣生成の衝突検知に使う。
        /// NovelScenarioRunner の AddCommand 一覧と対で更新すること。
        /// </summary>
        public static readonly IReadOnlyList<string> BuiltinCommandNames = new[]
        {
            "say", "choose", "flag", "portrait", "stage", "exit", "clear_stage",
            "bg", "still", "center_image", "hide_center_image", "se", "se_loop", "bgm",
            "wait", "world_effect", "message_window_visible", "clear_message",
            // VitalRouter.MRuby が Object に定義するメソッド
            "cmd", "state",
        };

#if UNITY_EDITOR
        /// <summary>RegisterNovelCommandSugars の DI ビルド時キャプチャ (Novel.Editor の生成器が購読)。</summary>
        public static event Action<IReadOnlyList<CommandKeyInfo>>? Captured;

        public static void Publish(IReadOnlyList<CommandKeyInfo> commands) => Captured?.Invoke(commands);
#endif
    }
}
