#nullable enable
using MRubyCS.Serializer;
using Novel.Runtime;
using VitalRouter;

namespace Novel.Cinematic
{
    /// <summary>
    /// <c>cinematic :key</c> / <c>cinematic_stop :key</c>。Resources 規約で置かれた CinematicSequenceAsset を再生する。
    /// world_effect とは別語彙 (あちらはゲーム側 sink の解釈、こちらはアセット駆動)。
    /// </summary>
    [MRubyObject]
    [NovelDescription("Resources/Novel/Effects/<key>.asset の演出を再生する")]
    public readonly partial record struct CinematicCommand : ICommand
    {
        [NovelDescription("アセット名 (Project Reference の「演出」タブ)")]
        public string Key { get; init; }

        [NovelDescription("true なら <key>_exit.asset を再生して止める")]
        public bool Stop { get; init; }
    }
}
