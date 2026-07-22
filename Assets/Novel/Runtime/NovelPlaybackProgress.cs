#nullable enable
namespace Novel.Runtime
{
    // say 通し番号と早送り状態を runner とハンドラで共有する (セリフ単位セーブのカーソルの源)
    public sealed class NovelPlaybackProgress
    {
        // 現在の再生（PlayAsync 1 回）内で処理を開始した say の通し番号（1 始まり。0 = 未開始）
        public int SayNumber { get; private set; }

        // 早送り中か。say 以外のコマンド（wait / se / world_effect 等）が演出スキップの判定に使う
        public bool IsFastForwarding => _fastForwardTarget > 0;

        // 通常表示へ戻す say 番号。0 = 早送りなし
        private int _fastForwardTarget;

        internal void Reset(int fastForwardTarget)
        {
            SayNumber = 0;
            _fastForwardTarget = fastForwardTarget;
        }

        // 戻り値: この say を表示スキップすべきか。目標到達の say 自身は通常表示に戻す (プレイヤーが保存した行を再表示するため)
        internal bool AdvanceSay()
        {
            SayNumber++;
            if (_fastForwardTarget <= 0) return false;
            if (SayNumber >= _fastForwardTarget)
            {
                _fastForwardTarget = 0;
                return false;
            }
            return true;
        }
    }
}
