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
            public string AudioChannelType { get; }
            public string PortraitChannelType { get; }
            public string CharacterCatalogType { get; }
            public DateTime CapturedAt { get; }

            public Snapshot(IReadOnlyList<AudioKeyInfo> audioKeys, IReadOnlyList<StageLayoutInfo> layouts,
                IReadOnlyList<CharacterKeyInfo> characters,
                string audioChannelType, string portraitChannelType, string characterCatalogType, DateTime capturedAt)
            {
                AudioKeys = audioKeys;
                Layouts = layouts;
                Characters = characters;
                AudioChannelType = audioChannelType;
                PortraitChannelType = portraitChannelType;
                CharacterCatalogType = characterCatalogType;
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
