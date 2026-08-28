#nullable enable
using MRubyCS.Serializer;
using VitalRouter;

namespace Novel.Cinematic
{
    /// <summary>
    /// <c>cinematic :key</c> / <c>cinematic_stop :key</c>。Resources 規約で置かれた CinematicSequenceAsset を再生する。
    /// world_effect とは別語彙 (あちらはゲーム側 sink の解釈、こちらはアセット駆動)。
    /// </summary>
    [MRubyObject]
    public readonly partial record struct CinematicCommand : ICommand
    {
        public string Key { get; init; }
        public bool Stop { get; init; }
    }
}
