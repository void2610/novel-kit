#nullable enable
using System;
using System.Collections.Generic;
using Novel.Editor;
using Novel.Runtime;
using UnityEditor;
using VitalRouter;
using MRubyCS;
using VitalRouter.MRuby;

namespace Novel.Cinematic.Editor
{
    /// <summary>プロジェクトリファレンスの「演出」タブと Validate Scenarios の cinematic キー検証を差し込む。</summary>
    [InitializeOnLoad]
    internal static class CinematicEffectReference
    {
        static CinematicEffectReference()
        {
            ProjectReferenceSections.Register(new Section());
            ScenarioKeyExtensions.Register(new KeyExtension());
        }

        private sealed class Section : IProjectReferenceSection
        {
            private List<CinematicEffectCatalog.Entry>? _entries;
            private List<CinematicEffectCatalog.Entry> Entries => _entries ??= CinematicEffectCatalog.Scan();

            public string Title => "演出";
            public int Count => Entries.Count;
            public void Invalidate() => _entries = null;

            public void Draw(ProjectReferenceWindow.Rows rows)
            {
                rows.DrawNote($"キー = Resources/{ResourcesCinematicSequenceLoader.Root} 配下のアセット名。cinematic :key / cinematic_stop :key で呼ぶ。");
                if (Entries.Count == 0)
                {
                    rows.DrawInfo($"Resources/{ResourcesCinematicSequenceLoader.Root} に CinematicSequenceAsset がありません (Create > Cinematic > Sequence Asset)。");
                    return;
                }
                foreach (var e in Entries)
                {
                    if (!rows.Matches(e.Key)) continue;
                    rows.DrawKeyRow(e.Key, $"{e.StepCount} ステップ    停止: {e.ExitKind}", e.AssetPath);
                }
            }
        }

        private sealed class KeyExtension : IScenarioKeyExtension
        {
            public string Label => "演出キー";
            public INovelCommandModule CreateRecorder(ISet<string> keys) => new CinematicKeyRecorder(keys);
            public IEnumerable<IPreambleSource> PreambleSources()
            {
                yield return new PreambleSource(new Novel.View.ResourcesTextAssetLoader(), CinematicCommandModule.PreambleKey);
            }
            public HashSet<string>? ScanKnownKeys()
            {
                var keys = CinematicEffectCatalog.KnownKeys();
                return keys.Count > 0 ? keys : null;   // 0 件 = 情報源なしとみなしスキップ (大量誤警告の回避)
            }
        }

    }

    // 語彙だけ登録してキーを記録する。Director 等の実体は要らない (VitalRouter の [Routes] は入れ子型を許さないため最上位に置く)
    [Routes]
    internal sealed partial class CinematicKeyRecorder : INovelCommandModule
    {
        private readonly ISet<string> _keys;
        public CinematicKeyRecorder(ISet<string> keys) => _keys = keys;
        public void RegisterVocabulary(MRubyState state) => state.AddCommand<CinematicCommand>("cinematic");
        public IDisposable MapHandlers(ICommandSubscribable router) => MapTo(router);
        public void On(CinematicCommand cmd)
        {
            if (!string.IsNullOrEmpty(cmd.Key)) _keys.Add(cmd.Key);
        }
    }
}
