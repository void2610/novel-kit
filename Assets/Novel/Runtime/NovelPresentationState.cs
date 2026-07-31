#nullable enable
namespace Novel.Runtime
{
    /// <summary>
    /// 提示中の盤面のうち、シナリオの再実行では戻せないものを runtime が保持する。
    ///
    /// 立ち絵・メッセージは途中復帰の早送りで再構築されるが、背景は違う。ノベルパートを抜けた先
    /// (パズル画面等) では bg コマンドが走らないため、game がロード後に自分で戻すしかない。
    /// キーを知っているのは runtime なので、追跡を game 側に強いず <see cref="NovelStateSnapshot"/> に載せる
    /// </summary>
    public sealed class NovelPresentationState
    {
        /// <summary>実表示中の背景キー (未表示・消去は空文字)</summary>
        public string BackgroundKey { get; private set; } = "";

        internal void SetBackground(string key) => BackgroundKey = key ?? "";
    }
}
