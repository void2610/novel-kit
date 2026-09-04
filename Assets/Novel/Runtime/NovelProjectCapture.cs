#nullable enable
#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using Novel.Assets;

namespace Novel.Runtime
{
    /// <summary>
    /// DI ビルド時に「実際に配線されたチャンネル」から吸い上げたプロジェクト情報のエディタ向け受け口
    /// (project-reference ADR)。RegisterNovelKitCore の build callback が <see cref="Publish"/> し、
    /// Novel.Editor が購読して永続化・プロジェクトリファレンスウィンドウに表示する。
    /// エディタ専用 (プレイヤービルドには含まれない)。game 側が触る必要はない。
    /// </summary>
    public static class NovelProjectCapture
    {
        public sealed class Snapshot
        {
            public IReadOnlyList<AudioKeyInfo> AudioKeys { get; }
            public IReadOnlyList<StageLayoutInfo> Layouts { get; }
            public IReadOnlyList<CharacterKeyInfo> Characters { get; }

            /// <summary>プロジェクト定義コマンド (INovelCommandModule の語彙)。</summary>
            public IReadOnlyList<CommandKeyInfo> Commands { get; }

            /// <summary>再生時に読み込んだ preamble (糖衣の定義元)。再生 1 回目の preamble ロード時にキャプチャされる。</summary>
            public IReadOnlyList<PreambleInfo> Preambles { get; }

            /// <summary>world_effect キーの目録 (IWorldEffectSink.EnumerateKeys)。</summary>
            public IReadOnlyList<WorldEffectKeyInfo> WorldEffectKeys { get; }
            public string WorldEffectSinkType { get; }
            public string AudioChannelType { get; }
            public string PortraitChannelType { get; }
            public string CharacterCatalogType { get; }

            /// <summary>配線されていた <see cref="ISpriteLoader"/> の型名 (未キャプチャなら空)。</summary>
            public string SpriteLoaderType { get; }

            /// <summary>
            /// ローダがキーの前に付けるプレフィックス。ローダが <see cref="ISpriteKeyPrefix"/> を
            /// 実装していなければ null (= 不明。空文字の「プレフィックス無しが確定」とは区別する)。
            /// </summary>
            public string? SpriteKeyPrefix { get; }

            public DateTime CapturedAt { get; }

            public Snapshot(IReadOnlyList<AudioKeyInfo> audioKeys, IReadOnlyList<StageLayoutInfo> layouts,
                IReadOnlyList<CharacterKeyInfo> characters,
                string audioChannelType, string portraitChannelType, string characterCatalogType, DateTime capturedAt,
                string spriteLoaderType = "", string? spriteKeyPrefix = null,
                IReadOnlyList<CommandKeyInfo>? commands = null,
                IReadOnlyList<PreambleInfo>? preambles = null,
                IReadOnlyList<WorldEffectKeyInfo>? worldEffectKeys = null, string worldEffectSinkType = "")
            {
                Commands = commands ?? Array.Empty<CommandKeyInfo>();
                Preambles = preambles ?? Array.Empty<PreambleInfo>();
                WorldEffectKeys = worldEffectKeys ?? Array.Empty<WorldEffectKeyInfo>();
                WorldEffectSinkType = worldEffectSinkType;
                AudioKeys = audioKeys;
                Layouts = layouts;
                Characters = characters;
                AudioChannelType = audioChannelType;
                PortraitChannelType = portraitChannelType;
                CharacterCatalogType = characterCatalogType;
                SpriteLoaderType = spriteLoaderType;
                SpriteKeyPrefix = spriteKeyPrefix;
                CapturedAt = capturedAt;
            }
        }

        /// <summary>このドメインで最後にキャプチャされたスナップショット (未キャプチャなら null)。</summary>
        public static Snapshot? Latest { get; private set; }

        /// <summary>キャプチャのたびに発火する (Novel.Editor の永続化が購読)。</summary>
        public static event Action<Snapshot>? Captured;

        public static void Publish(Snapshot snapshot)
        {
            Latest = snapshot;
            Captured?.Invoke(snapshot);
        }
    }
}
#endif
