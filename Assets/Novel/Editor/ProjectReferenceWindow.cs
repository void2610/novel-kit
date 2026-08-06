#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using Novel.Assets;
using Novel.Runtime;
using Novel.View;
using UnityEditor;
using UnityEngine;

namespace Novel.Editor
{
    /// <summary>
    /// このプロジェクトのシナリオで使える「名前と構図」を一覧するウィンドウ (project-reference ADR)。
    /// アセットから静的に読めるもの (キャラ・画像キー・音カタログ) はライブ表示し、
    /// 実行時にしか実体がないもの (自前チャンネルの音キー・構図) は DI ビルド時キャプチャ
    /// (最後に再生した時点のスナップショット) を表示する。
    /// </summary>
    public sealed class ProjectReferenceWindow : EditorWindow
    {
        [MenuItem("Novel/Project Reference")]
        public static void Open() => GetWindow<ProjectReferenceWindow>("Novel Reference");

        private Vector2 _scroll;
        private string _search = "";
        private bool _showCharacters = true;
        private bool _showImages = true;
        private bool _showLayouts = true;
        private bool _showAudio = true;

        // スキャン結果キャッシュ。null なら次の描画で再構築
        private List<CharacterCatalogView>? _characters;
        private List<ImageGroup>? _imageGroups;
        private List<AudioRow>? _audioRows;

        private sealed class CharacterCatalogView
        {
            public string AssetPath = "";
            public List<(string id, string displayName, string defaultPortrait)> Entries = new();
        }

        private sealed class ImageGroup
        {
            public string Folder = "";
            public List<string> Keys = new();
        }

        private sealed class AudioRow
        {
            public AudioKeyKind Kind;
            public string Key = "";
            public string Note = "";
            public string Source = "";
        }

        private void OnFocus() => Invalidate();
        private void OnProjectChange() => Invalidate();

        private void Invalidate()
        {
            _characters = null;
            _imageGroups = null;
            _audioRows = null;
            Repaint();
        }

        private void OnGUI()
        {
            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                if (GUILayout.Button("更新", EditorStyles.toolbarButton, GUILayout.Width(60))) Invalidate();
                _search = GUILayout.TextField(_search, EditorStyles.toolbarSearchField);
            }

            _scroll = EditorGUILayout.BeginScrollView(_scroll);
            DrawCharacters();
            DrawImages();
            DrawLayouts();
            DrawAudio();
            EditorGUILayout.EndScrollView();
        }

        private bool Matches(string text) =>
            string.IsNullOrEmpty(_search) || text.IndexOf(_search, StringComparison.OrdinalIgnoreCase) >= 0;

        // ---- キャラ (ScriptableCharacterCatalog をライブ表示) ----

        private void DrawCharacters()
        {
            _characters ??= ScanCharacters();
            _showCharacters = EditorGUILayout.Foldout(_showCharacters, $"キャラ ({_characters.Sum(c => c.Entries.Count)})", true);
            if (!_showCharacters) return;

            using var _ = new EditorGUI.IndentLevelScope();
            if (_characters.Count == 0)
            {
                EditorGUILayout.HelpBox("ScriptableCharacterCatalog が見つかりません (Create > Novel > Character Catalog)。", MessageType.Info);
                return;
            }
            foreach (var catalog in _characters)
            {
                EditorGUILayout.LabelField(catalog.AssetPath, EditorStyles.miniBoldLabel);
                foreach (var (id, displayName, defaultPortrait) in catalog.Entries)
                {
                    if (!Matches(id) && !Matches(displayName)) continue;
                    var portrait = string.IsNullOrEmpty(defaultPortrait) ? "" : $"  既定立ち絵: {defaultPortrait}";
                    EditorGUILayout.LabelField($"{id}", $"表示名: {displayName}{portrait}");
                }
            }
        }

        private static List<CharacterCatalogView> ScanCharacters()
        {
            var result = new List<CharacterCatalogView>();
            foreach (var guid in AssetDatabase.FindAssets("t:ScriptableCharacterCatalog"))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var asset = AssetDatabase.LoadAssetAtPath<ScriptableCharacterCatalog>(path);
                if (asset == null) continue;

                var view = new CharacterCatalogView { AssetPath = path };
                // entries は private serialized のため SerializedObject 経由で読む (ランタイム型に editor 用の口を足さない)
                var so = new SerializedObject(asset);
                var entries = so.FindProperty("entries");
                if (entries != null)
                {
                    for (var i = 0; i < entries.arraySize; i++)
                    {
                        var e = entries.GetArrayElementAtIndex(i);
                        var id = e.FindPropertyRelative("speakerId")?.stringValue ?? "";
                        if (string.IsNullOrEmpty(id)) continue;
                        var name = e.FindPropertyRelative("displayName")?.stringValue ?? "";
                        view.Entries.Add((id, string.IsNullOrEmpty(name) ? id : name,
                            e.FindPropertyRelative("defaultPortraitKey")?.stringValue ?? ""));
                    }
                }
                result.Add(view);
            }
            return result;
        }

        // ---- 画像キー (Resources のスプライトをライブ表示。キー = Resources 相対パス) ----

        private void DrawImages()
        {
            _imageGroups ??= ScanImages();
            _showImages = EditorGUILayout.Foldout(_showImages, $"画像キー ({_imageGroups.Sum(g => g.Keys.Count)})", true);
            if (!_showImages) return;

            using var _ = new EditorGUI.IndentLevelScope();
            EditorGUILayout.LabelField("キー = Resources 相対パス (立ち絵/背景/一枚絵/補足画像 共通)", EditorStyles.miniLabel);
            if (_imageGroups.Count == 0)
            {
                EditorGUILayout.HelpBox("Resources 配下にスプライトが見つかりません。", MessageType.Info);
                return;
            }
            foreach (var group in _imageGroups)
            {
                var keys = group.Keys.Where(Matches).ToList();
                if (keys.Count == 0) continue;
                EditorGUILayout.LabelField($"{group.Folder} ({keys.Count})", EditorStyles.miniBoldLabel);
                foreach (var key in keys)
                    EditorGUILayout.LabelField(key);
            }
        }

        private static List<ImageGroup> ScanImages()
        {
            const string marker = "/Resources/";
            var keys = new SortedSet<string>(StringComparer.Ordinal);
            foreach (var guid in AssetDatabase.FindAssets("t:Sprite"))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var at = path.IndexOf(marker, StringComparison.Ordinal);
                if (at < 0) continue;
                if (path.Contains("/Tests/")) continue;   // テスト用アセットは実行時に存在しない
                var relative = path.Substring(at + marker.Length);
                var dot = relative.LastIndexOf('.');
                keys.Add(dot >= 0 ? relative.Substring(0, dot) : relative);
            }

            return keys
                .GroupBy(k =>
                {
                    var slash = k.IndexOf('/');
                    return slash < 0 ? "(ルート直下)" : k.Substring(0, slash);
                })
                .Select(g => new ImageGroup { Folder = g.Key, Keys = g.ToList() })
                .OrderBy(g => g.Folder, StringComparer.Ordinal)
                .ToList();
        }

        // ---- 構図 (DI ビルド時キャプチャ。未キャプチャなら標準構図) ----

        private void DrawLayouts()
        {
            var snapshot = ProjectReferenceCaptureStore.LoadOrLatest();
            var layouts = snapshot?.Layouts ?? StageLayoutInfo.Defaults;
            _showLayouts = EditorGUILayout.Foldout(_showLayouts, $"構図 ({layouts.Count})", true);
            if (!_showLayouts) return;

            using var _ = new EditorGUI.IndentLevelScope();
            EditorGUILayout.LabelField(
                snapshot == null
                    ? "未キャプチャのため標準構図を表示中 (一度再生すると実際の配線から取得)"
                    : $"取得元: {snapshot.PortraitChannelType} ({FormatTime(snapshot.CapturedAt)} の再生時)",
                EditorStyles.miniLabel);
            foreach (var layout in layouts)
            {
                if (!Matches(layout.Id)) continue;
                var note = string.IsNullOrEmpty(layout.Note) ? "" : $"  {layout.Note}";
                EditorGUILayout.LabelField($":{layout.Id}", $"{layout.SlotCount} 人{note}");
            }
        }

        // ---- BGM / SE (音カタログのライブ表示 + DI ビルド時キャプチャ) ----

        private void DrawAudio()
        {
            _audioRows ??= ScanAudio();
            _showAudio = EditorGUILayout.Foldout(_showAudio, $"BGM / SE ({_audioRows.Count})", true);
            if (!_showAudio) return;

            using var _ = new EditorGUI.IndentLevelScope();
            if (_audioRows.Count == 0)
            {
                EditorGUILayout.HelpBox(
                    "音キーが見つかりません。参考 ScriptableAudioCatalog を作る (Create > Novel > Audio Catalog) か、" +
                    "自前 IAudioChannel の EnumerateKeys() を実装して一度再生してください。", MessageType.Info);
                return;
            }
            foreach (var kind in new[] { AudioKeyKind.Bgm, AudioKeyKind.Se })
            {
                var rows = _audioRows.Where(r => r.Kind == kind && (Matches(r.Key) || Matches(r.Note))).ToList();
                if (rows.Count == 0) continue;
                EditorGUILayout.LabelField($"{(kind == AudioKeyKind.Bgm ? "BGM" : "SE")} ({rows.Count})", EditorStyles.miniBoldLabel);
                foreach (var row in rows)
                {
                    var note = string.IsNullOrEmpty(row.Note) ? "" : $"  {row.Note}";
                    EditorGUILayout.LabelField(row.Key, $"{note}  [{row.Source}]");
                }
            }
        }

        private static List<AudioRow> ScanAudio()
        {
            var rows = new List<AudioRow>();
            var seen = new HashSet<(AudioKeyKind, string)>();

            // 参考カタログのアセットは再生不要でライブ表示できる
            foreach (var guid in AssetDatabase.FindAssets("t:ScriptableAudioCatalog"))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var asset = AssetDatabase.LoadAssetAtPath<ScriptableAudioCatalog>(path);
                if (asset == null) continue;
                foreach (var key in asset.EnumerateKeys())
                    if (seen.Add((key.Kind, key.Key)))
                        rows.Add(new AudioRow { Kind = key.Kind, Key = key.Key, Note = key.Note ?? "", Source = System.IO.Path.GetFileNameWithoutExtension(path) });
            }

            // 自前チャンネル等、実行時にしか実体がないものはキャプチャから
            var snapshot = ProjectReferenceCaptureStore.LoadOrLatest();
            if (snapshot != null)
            {
                var source = $"{snapshot.AudioChannelType} ({FormatTime(snapshot.CapturedAt)} の再生時)";
                foreach (var key in snapshot.AudioKeys)
                    if (seen.Add((key.Kind, key.Key)))
                        rows.Add(new AudioRow { Kind = key.Kind, Key = key.Key, Note = key.Note ?? "", Source = source });
            }

            rows.Sort((a, b) => a.Kind != b.Kind ? a.Kind.CompareTo(b.Kind) : string.CompareOrdinal(a.Key, b.Key));
            return rows;
        }

        private static string FormatTime(DateTime time) =>
            time == default ? "?" : time.ToString("MM/dd HH:mm");
    }
}
