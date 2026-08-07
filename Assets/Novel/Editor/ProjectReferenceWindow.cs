#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
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
    /// 画像・立ち絵はサムネイル付き、音キーは Resources 上のクリップへ解決できればその場で試聴できる。
    /// </summary>
    public sealed class ProjectReferenceWindow : EditorWindow
    {
        [MenuItem("Novel/Project Reference")]
        public static void Open() => GetWindow<ProjectReferenceWindow>("Novel Reference");

        private enum Tab
        {
            Characters,
            Images,
            Layouts,
            Audio,
        }

        private Vector2 _scroll;
        private string _search = "";
        private Tab _tab;
        private float _thumbSize = 48f;

        // スキャン結果キャッシュ。null なら次の描画で再構築
        private List<CharacterCatalogView>? _characters;
        private List<ImageGroup>? _imageGroups;
        private Dictionary<string, string>? _spritePathByKey;   // Resources 相対キー → アセットパス
        private Dictionary<string, string>? _audioPathByKey;    // Resources 相対キー → AudioClip アセットパス
        private readonly Dictionary<string, string?> _resolvedSprites = new();  // 論理キー → アセットパス (負キャッシュ込み)
        private readonly Dictionary<string, string?> _resolvedAudio = new();

        private AudioClip? _playingClip;

        private static GUIStyle? _rowLabel;
        private static GUIStyle RowLabel => _rowLabel ??= new GUIStyle(EditorStyles.label) { alignment = TextAnchor.MiddleLeft };

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

        private void OnEnable() => AssetPreview.SetPreviewTextureCacheSize(256);
        private void OnDisable() => AudioPreviewUtil.StopAll();
        private void OnFocus() => Invalidate();
        private void OnProjectChange() => Invalidate();

        // 試聴が鳴り終わったら ▶ 表示へ戻すため、再生中は低頻度で再描画する
        private void OnInspectorUpdate()
        {
            if (_playingClip != null) Repaint();
        }

        private void Invalidate()
        {
            _characters = null;
            _imageGroups = null;
            _spritePathByKey = null;
            _audioPathByKey = null;
            _resolvedSprites.Clear();
            _resolvedAudio.Clear();
            Repaint();
        }

        private void OnGUI()
        {
            if (_playingClip != null && !AudioPreviewUtil.IsPlaying(_playingClip)) _playingClip = null;

            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                if (GUILayout.Button("更新", EditorStyles.toolbarButton, GUILayout.Width(60))) Invalidate();
                _search = GUILayout.TextField(_search, EditorStyles.toolbarSearchField);
                if (_tab is Tab.Characters or Tab.Images)
                    _thumbSize = GUILayout.HorizontalSlider(_thumbSize, 24f, 96f, GUILayout.Width(80));
            }

            // スキャンはツールバーの後で行う (「更新」の Invalidate と同フレームで null を踏まないため)
            _characters ??= ScanCharacters();
            _imageGroups ??= ScanImages();

            var snapshot = ProjectReferenceCaptureStore.LoadOrLatest();
            var layouts = snapshot?.Layouts ?? StageLayoutInfo.Defaults;
            var audioKeys = snapshot?.AudioKeys ?? Array.Empty<AudioKeyInfo>();

            var labels = new[]
            {
                $"キャラ ({_characters.Sum(c => c.Entries.Count)})",
                $"画像 ({_imageGroups.Sum(g => g.Keys.Count)})",
                $"構図 ({layouts.Count})",
                $"BGM / SE ({audioKeys.Count})",
            };
            var tab = (Tab)GUILayout.Toolbar((int)_tab, labels);
            if (tab != _tab)
            {
                _tab = tab;
                _scroll = Vector2.zero;
                GUI.FocusControl(null);
            }

            _scroll = EditorGUILayout.BeginScrollView(_scroll);
            switch (_tab)
            {
                case Tab.Characters: DrawCharacters(); break;
                case Tab.Images: DrawImages(); break;
                case Tab.Layouts: DrawLayouts(snapshot, layouts); break;
                case Tab.Audio: DrawAudio(snapshot, audioKeys); break;
            }
            EditorGUILayout.EndScrollView();
        }

        private bool Matches(string text) =>
            string.IsNullOrEmpty(_search) || text.IndexOf(_search, StringComparison.OrdinalIgnoreCase) >= 0;

        // スクロール外の行はサムネイル要求を出さない (AssetPreview キャッシュの浪費防止)
        private bool IsRowVisible(Rect rect) =>
            rect.yMax >= _scroll.y - 100f && rect.y <= _scroll.y + position.height + 100f;

        // ---- キャラ (ScriptableCharacterCatalog をライブ表示) ----

        private void DrawCharacters()
        {
            if (_characters!.Count == 0)
            {
                EditorGUILayout.HelpBox("ScriptableCharacterCatalog が見つかりません (Create > Novel > Character Catalog)。", MessageType.Info);
                return;
            }
            foreach (var catalog in _characters)
            {
                DrawPingableHeader(catalog.AssetPath, catalog.AssetPath);
                foreach (var (id, displayName, defaultPortrait) in catalog.Entries)
                {
                    if (!Matches(id) && !Matches(displayName)) continue;
                    var portrait = string.IsNullOrEmpty(defaultPortrait) ? "" : $"  既定立ち絵: {defaultPortrait}";
                    DrawThumbnailRow(ResolveSpritePath(defaultPortrait), id, $"表示名: {displayName}{portrait}");
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
            EditorGUILayout.LabelField("キー = Resources 相対パス (立ち絵/背景/一枚絵/補足画像 共通)。クリックでアセットを選択。", EditorStyles.miniLabel);
            if (_imageGroups!.Count == 0)
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
                    DrawThumbnailRow(_spritePathByKey!.GetValueOrDefault(key), key, "");
            }
        }

        private List<ImageGroup> ScanImages()
        {
            const string marker = "/Resources/";
            var pathByKey = new Dictionary<string, string>(StringComparer.Ordinal);
            var keys = new SortedSet<string>(StringComparer.Ordinal);
            foreach (var guid in AssetDatabase.FindAssets("t:Sprite"))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var at = path.IndexOf(marker, StringComparison.Ordinal);
                // テスト用・Editor 配下のアセットは実行時に存在しない (ScenarioKeyValidator と同じ除外)
                if (at < 0 || path.Contains("/Tests/") || path.Contains("/Editor/")) continue;
                var relative = path.Substring(at + marker.Length);
                var dot = relative.LastIndexOf('.');
                var key = dot >= 0 ? relative.Substring(0, dot) : relative;
                keys.Add(key);
                pathByKey[key] = path;
            }
            _spritePathByKey = pathByKey;

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

        /// <summary>
        /// 論理キーを Resources 上のスプライトへ解決する。ローダの root プレフィックスは編集モードでは
        /// 分からないため、完全一致の次に後方一致で照合する (ScenarioKeyValidator と同じ割り切り)。
        /// </summary>
        private string? ResolveSpritePath(string key)
        {
            if (string.IsNullOrEmpty(key)) return null;
            if (_resolvedSprites.TryGetValue(key, out var cached)) return cached;
            var path = ResolveByKey(_spritePathByKey!, key);
            _resolvedSprites[key] = path;
            return path;
        }

        private static string? ResolveByKey(Dictionary<string, string> pathByKey, string key)
        {
            if (pathByKey.TryGetValue(key, out var exact)) return exact;
            var suffix = "/" + key;
            string? found = null;
            foreach (var (candidate, path) in pathByKey)
            {
                if (!candidate.EndsWith(suffix, StringComparison.Ordinal)) continue;
                if (found != null) return null;   // 曖昧なら解決しない (誤ったサムネイルを出すより無い方がよい)
                found = path;
            }
            return found;
        }

        // ---- 構図 (DI ビルド時キャプチャ。未キャプチャなら標準構図) ----

        private void DrawLayouts(NovelProjectCapture.Snapshot? snapshot, IReadOnlyList<StageLayoutInfo> layouts)
        {
            EditorGUILayout.LabelField(
                snapshot == null
                    ? "未キャプチャのため標準構図を表示中 (一度再生すると実際の配線から取得)"
                    : $"取得元: {snapshot.PortraitChannelType} ({FormatTime(snapshot.CapturedAt)} の再生時)",
                EditorStyles.miniLabel);
            foreach (var layout in layouts)
            {
                if (!Matches(layout.Id)) continue;
                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.LabelField($":{layout.Id}", GUILayout.Width(120));
                    DrawSlotDiagram(layout.SlotCount);
                    var note = string.IsNullOrEmpty(layout.Note) ? "" : $"  {layout.Note}";
                    EditorGUILayout.LabelField($"{layout.SlotCount} 人{note}");
                }
            }
        }

        /// <summary>スロット数を「画面に何人立つか」のミニ図で示す (等間隔に配置)。</summary>
        private static void DrawSlotDiagram(int slotCount)
        {
            var rect = GUILayoutUtility.GetRect(120f, 24f, GUILayout.Width(120f));
            rect.y += 2f;
            rect.height -= 4f;
            EditorGUI.DrawRect(rect, EditorGUIUtility.isProSkin ? new Color(1f, 1f, 1f, 0.06f) : new Color(0f, 0f, 0f, 0.08f));
            if (slotCount <= 0) return;
            var slotColor = EditorGUIUtility.isProSkin ? new Color(0.5f, 0.8f, 1f, 0.9f) : new Color(0.15f, 0.4f, 0.7f, 0.9f);
            const float slotWidth = 10f;
            // SlotCount に契約上の上限はないため、図に収まる数へクランプする (実数は隣のラベルが示す)
            var drawn = Mathf.Min(slotCount, 8);
            for (var i = 0; i < drawn; i++)
            {
                var centerX = rect.x + rect.width * (i + 1) / (drawn + 1);
                EditorGUI.DrawRect(new Rect(centerX - slotWidth / 2f, rect.y + 3f, slotWidth, rect.height - 6f), slotColor);
            }
        }

        // ---- BGM / SE (DI ビルド時キャプチャ) ----

        private void DrawAudio(NovelProjectCapture.Snapshot? snapshot, IReadOnlyList<AudioKeyInfo> keys)
        {
            _audioPathByKey ??= ScanAudioClips();
            if (keys.Count == 0)
            {
                EditorGUILayout.HelpBox(
                    "音キーは IAudioChannel.EnumerateKeys() から取得します。自前チャンネルでオーバーライドし、一度再生してください。",
                    MessageType.Info);
                return;
            }
            EditorGUILayout.LabelField($"取得元: {snapshot!.AudioChannelType} ({FormatTime(snapshot.CapturedAt)} の再生時)", EditorStyles.miniLabel);
            foreach (var kind in new[] { AudioKeyKind.Bgm, AudioKeyKind.Se })
            {
                var rows = keys
                    .Where(k => k.Kind == kind && (Matches(k.Key) || Matches(k.Note ?? "")))
                    .OrderBy(k => k.Key, StringComparer.Ordinal)
                    .ToList();
                if (rows.Count == 0) continue;
                EditorGUILayout.LabelField($"{(kind == AudioKeyKind.Bgm ? "BGM" : "SE")} ({rows.Count})", EditorStyles.miniBoldLabel);
                foreach (var key in rows)
                    DrawAudioRow(key);
            }
        }

        private void DrawAudioRow(AudioKeyInfo key)
        {
            var clipPath = ResolveAudioPath(key.Key);
            var clip = clipPath == null ? null : AssetDatabase.LoadAssetAtPath<AudioClip>(clipPath);
            using (new EditorGUILayout.HorizontalScope())
            {
                if (clip != null)
                {
                    var playingThis = _playingClip == clip;
                    if (GUILayout.Button(playingThis ? "■" : "▶", GUILayout.Width(28f)))
                    {
                        AudioPreviewUtil.StopAll();
                        _playingClip = null;
                        if (!playingThis)
                        {
                            AudioPreviewUtil.Play(clip);
                            _playingClip = clip;
                        }
                    }
                }
                else
                {
                    // キーが Resources 上のクリップへ解決できない場合は試聴不可 (自前チャンネルのキー体系は編集モードでは分からない)
                    using (new EditorGUI.DisabledScope(true))
                        GUILayout.Button(new GUIContent("▶", "Resources 上に対応する AudioClip が見つからないため試聴できません"), GUILayout.Width(28f));
                }

                var duration = clip == null ? "" : $"  {(int)(clip.length / 60)}:{clip.length % 60:00.0}";
                var rect = GUILayoutUtility.GetRect(new GUIContent(key.Key), RowLabel, GUILayout.MinWidth(120f));
                GUI.Label(rect, key.Key, RowLabel);
                if (clip != null && Event.current.type == EventType.MouseDown && rect.Contains(Event.current.mousePosition))
                {
                    EditorGUIUtility.PingObject(clip);
                    Event.current.Use();
                }
                EditorGUILayout.LabelField($"{key.Note ?? ""}{duration}", EditorStyles.miniLabel);
            }
        }

        private static Dictionary<string, string> ScanAudioClips()
        {
            const string marker = "/Resources/";
            var pathByKey = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var guid in AssetDatabase.FindAssets("t:AudioClip"))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var at = path.IndexOf(marker, StringComparison.Ordinal);
                // テスト用・Editor 配下のアセットは実行時に存在しない (ScenarioKeyValidator と同じ除外)
                if (at < 0 || path.Contains("/Tests/") || path.Contains("/Editor/")) continue;
                var relative = path.Substring(at + marker.Length);
                var dot = relative.LastIndexOf('.');
                pathByKey[dot >= 0 ? relative.Substring(0, dot) : relative] = path;
            }
            return pathByKey;
        }

        /// <summary>音キーを Resources 上の AudioClip へ解決する (完全一致 → 後方一致。曖昧なら解決しない)。</summary>
        private string? ResolveAudioPath(string key)
        {
            if (string.IsNullOrEmpty(key)) return null;
            if (_resolvedAudio.TryGetValue(key, out var cached)) return cached;
            var path = ResolveByKey(_audioPathByKey!, key);
            _resolvedAudio[key] = path;
            return path;
        }

        // ---- 共通描画 ----

        /// <summary>サムネイル付きの 1 行。クリックでアセットを ping する。</summary>
        private void DrawThumbnailRow(string? assetPath, string label, string subLabel)
        {
            var size = Mathf.Round(_thumbSize);
            var rect = EditorGUILayout.GetControlRect(false, size);
            var thumbRect = new Rect(rect.x, rect.y + 1f, size, size - 2f);
            var labelRect = new Rect(thumbRect.xMax + 6f, rect.y, rect.width - size - 6f, rect.height);

            if (assetPath != null && IsRowVisible(rect))
            {
                var asset = AssetDatabase.LoadMainAssetAtPath(assetPath);
                var preview = asset == null ? null : AssetPreview.GetAssetPreview(asset);
                if (preview != null)
                {
                    GUI.DrawTexture(thumbRect, preview, ScaleMode.ScaleToFit);
                }
                else
                {
                    EditorGUI.DrawRect(thumbRect, new Color(0.5f, 0.5f, 0.5f, 0.1f));
                    if (asset != null && AssetPreview.IsLoadingAssetPreview(asset.GetInstanceID())) Repaint();
                }
            }
            else if (assetPath == null)
            {
                EditorGUI.DrawRect(thumbRect, new Color(0.5f, 0.5f, 0.5f, 0.1f));
            }

            var text = string.IsNullOrEmpty(subLabel) ? label : $"{label}    {subLabel}";
            GUI.Label(labelRect, text, RowLabel);

            if (assetPath != null && Event.current.type == EventType.MouseDown && rect.Contains(Event.current.mousePosition))
            {
                EditorGUIUtility.PingObject(AssetDatabase.LoadMainAssetAtPath(assetPath));
                Event.current.Use();
            }
        }

        /// <summary>クリックでアセットを ping できる見出しラベル。</summary>
        private static void DrawPingableHeader(string label, string assetPath)
        {
            var rect = EditorGUILayout.GetControlRect(false, EditorGUIUtility.singleLineHeight);
            GUI.Label(rect, label, EditorStyles.miniBoldLabel);
            if (Event.current.type == EventType.MouseDown && rect.Contains(Event.current.mousePosition))
            {
                EditorGUIUtility.PingObject(AssetDatabase.LoadMainAssetAtPath(assetPath));
                Event.current.Use();
            }
        }

        private static string FormatTime(DateTime time) =>
            time == default ? "?" : time.ToString("MM/dd HH:mm");

        /// <summary>
        /// エディタの試聴再生。公開 API が無いため UnityEditor.AudioUtil をリフレクションで呼ぶ
        /// (Unity 2020+ の PlayPreviewClip 系。見つからなければ静かに no-op)。
        /// </summary>
        private static class AudioPreviewUtil
        {
            private static readonly Type? Util = typeof(AudioImporter).Assembly.GetType("UnityEditor.AudioUtil");
            private static readonly MethodInfo? PlayMethod = Find("PlayPreviewClip", "PlayClip");
            private static readonly MethodInfo? StopMethod = Find("StopAllPreviewClips", "StopAllClips");
            private static readonly MethodInfo? IsPlayingMethod = Find("IsPreviewClipPlaying", "IsClipPlaying");

            private static MethodInfo? Find(params string[] names)
            {
                if (Util == null) return null;
                var methods = Util.GetMethods(BindingFlags.Static | BindingFlags.Public);
                return names
                    .SelectMany(n => methods.Where(m => m.Name == n).OrderByDescending(m => m.GetParameters().Length))
                    .FirstOrDefault();
            }

            public static void Play(AudioClip clip)
            {
                if (PlayMethod == null) return;
                var args = PlayMethod.GetParameters().Length switch
                {
                    1 => new object[] { clip },
                    2 => new object[] { clip, 0 },
                    3 => new object[] { clip, 0, false },
                    _ => null,
                };
                if (args != null) PlayMethod.Invoke(null, args);
            }

            public static void StopAll() => StopMethod?.Invoke(null, null);

            public static bool IsPlaying(AudioClip clip)
            {
                if (IsPlayingMethod == null) return false;
                var args = IsPlayingMethod.GetParameters().Length == 1 ? new object[] { clip } : null;
                return IsPlayingMethod.Invoke(null, args) is true;
            }
        }
    }
}
