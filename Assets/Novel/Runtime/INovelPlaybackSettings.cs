#nullable enable
namespace Novel.Runtime
{
    // 再生設定（タイプライタ速度・auto/skip）。game が差し替え可、未指定なら Default
    public interface INovelPlaybackSettings
    {
        float CharsPerSecond { get; }      // タイプライタの表示速度（文字/秒）
        float AutoAdvanceDelay { get; }    // auto 進行時の行末待ち（秒）
        bool SkipUnread { get; }           // skip 時に未読行も飛ばすか
    }

    public sealed class DefaultNovelPlaybackSettings : INovelPlaybackSettings
    {
        public float CharsPerSecond => 30f;
        public float AutoAdvanceDelay => 1.5f;
        public bool SkipUnread => false;
    }
}
