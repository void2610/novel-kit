#nullable enable
using MRubyCS.Serializer;
using VitalRouter;

namespace Novel.Commands
{
    // リッチ統一語彙。未配線コマンドはハンドラ側で no-op（dsl-vocabulary）。引数詳細は実装段階の暫定

    // 選択肢提示 → 選んだ index を StateKey に書き戻し、Ruby は state[:key] で分岐する
    [MRubyObject]
    public readonly partial record struct ChooseCommand : ICommand
    {
        public string[] Options { get; init; }
        public string StateKey { get; init; }
    }

    // フラグ/変数設定（単一 int 名前空間）
    [MRubyObject]
    public readonly partial record struct FlagCommand : ICommand
    {
        public string Key { get; init; }
        public int Value { get; init; }
    }

    // 立ち絵: 単一スロットに 1 枚差し替え
    [MRubyObject]
    public readonly partial record struct PortraitCommand : ICommand
    {
        public string Character { get; init; }
        public string PortraitKey { get; init; }
    }

    // 背景差し替え
    [MRubyObject]
    public readonly partial record struct BackgroundCommand : ICommand
    {
        public string BackgroundKey { get; init; }
    }

    // イベント CG（一枚絵）
    [MRubyObject]
    public readonly partial record struct StillCommand : ICommand
    {
        public string StillKey { get; init; }
    }

    // 効果音
    [MRubyObject]
    public readonly partial record struct SeCommand : ICommand
    {
        public string SeKey { get; init; }
    }

    // BGM（空文字 = 停止）
    [MRubyObject]
    public readonly partial record struct BgmCommand : ICommand
    {
        public string BgmKey { get; init; }
    }

    // 明示待機（秒）
    [MRubyObject]
    public readonly partial record struct WaitCommand : ICommand
    {
        public float Seconds { get; init; }
    }
}
