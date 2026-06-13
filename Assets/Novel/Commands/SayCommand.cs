#nullable enable
using MRubyCS.Serializer;
using VitalRouter;

namespace Novel.Commands
{
    // 行コマンドのプリミティブ。キャラ名/narration は preamble.rb の糖衣が say へ落とす
    [MRubyObject]
    public readonly partial record struct SayCommand : ICommand
    {
        public string SpeakerId { get; init; }   // "" / null = ナレーション
        public string? DisplayAs { get; init; }   // 任意: 表示名の上書き（名前リビール）
        public string Text { get; init; }         // インラインタグ生テキスト（Runtime で字句解析）
    }
}
