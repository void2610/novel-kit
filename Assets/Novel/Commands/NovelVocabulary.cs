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

    // 立ち絵: stage 宣言で割り当てられたスロットに 1 枚差し替える。 slot 位置は IPortraitDirector が解決する。
    [MRubyObject]
    public readonly partial record struct PortraitCommand : ICommand
    {
        public string Character { get; init; }
        public string PortraitKey { get; init; }
    }

    // 場面 (cast) を宣言。 LayoutId + 配列順の cast で「キャラ → slot index (0..N-1)」を一括設定する。
    // 配列を渡す形 (stage :trio, [:a, :b, :c]) と hash を渡す形 (stage :trio, a: 0, b: 2, c: 1) は preamble で吸収。
    // ここではフラット配列で受け取り、 偶数 index に Character、 奇数 index に SlotIndex の文字列を並べる
    // (MRuby と C# の橋渡しでヘテロ配列より安定するため)。
    [MRubyObject]
    public readonly partial record struct StageCommand : ICommand
    {
        public string LayoutId { get; init; }
        // 例: ["taylor", "0", "kii", "1", "protagonist", "2"]
        public string[] CastPairs { get; init; }
    }

    // 指定キャラを場面から退場 (cast から外し、 該当 slot を非表示に)
    [MRubyObject]
    public readonly partial record struct ExitCommand : ICommand
    {
        public string Character { get; init; }
    }

    // すべての cast をクリアして場面をリセット
    [MRubyObject]
    public readonly partial record struct ClearStageCommand : ICommand
    {
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

    // 世界エフェクト（カメラ/画面/gameplay への脱出）。blocking 性は game の sink が返すタスクで決まる（effect-await）
    [MRubyObject]
    public readonly partial record struct WorldEffectCommand : ICommand
    {
        public string EffectKey { get; init; }
        public float[] Args { get; init; }
    }
}
