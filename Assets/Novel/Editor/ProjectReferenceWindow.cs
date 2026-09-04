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
            Commands,
        }

        private Vector2 _scroll;
        private string _search = "";
        private int _tabIndex;
        private Tab _tab => (Tab)_tabIndex;
        private Rows? _rows;
        private float _thumbSize = 48f;

        // スキャン結果キャッシュ。null なら次の描画で再構築
        private List<CharacterCatalogView>? _characters;
        private List<ImageGroup>? _imageGroups;
        private Dictionary<string, string>? _spritePathByKey;   // Resources 相対キー → アセットパス
        private Dictionary<string, string>? _audioPathByKey;    // Resources 相対キー → AudioClip アセットパス
        private readonly Dictionary<string, string?> _resolvedSprites = new();  // 論理キー → アセットパス (負キャッシュ込み)
        private readonly Dictionary<string, string?> _resolvedAudio = new();

        private AudioClip? _playingClip;

        // 最新キャプチャ由来のスプライトローダ情報 (null = ローダが ISpriteKeyPrefix を名乗っていない = 不明)
        private string? _keyPrefix;
        private string _spriteLoaderType = "";
        private List<string>? _scenarioSpriteKeys;   // root を剥がした「シナリオに書けるキー」全件
        private readonly Dictionary<string, IReadOnlyList<PortraitKeyRow>> _portraitRows = new();

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
            foreach (var section in ProjectReferenceSections.All) section.Invalidate();
            PreambleSourceCatalog.Invalidate();
            _characters = null;
            _imageGroups = null;
            _spritePathByKey = null;
            _audioPathByKey = null;
            _scenarioSpriteKeys = null;
            _portraitRows.Clear();
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
                if (_tabIndex >= BuiltinTabCount || _tab is Tab.Characters or Tab.Images)
                    _thumbSize = GUILayout.HorizontalSlider(_thumbSize, 24f, 96f, GUILayout.Width(80));
            }

            // スキャンはツールバーの後で行う (「更新」の Invalidate と同フレームで null を踏まないため)
            _characters ??= ScanCharacters();
            _imageGroups ??= ScanImages();

            var snapshot = ProjectReferenceCaptureStore.LoadOrLatest();
            var layouts = snapshot?.Layouts ?? StageLayoutInfo.Defaults;
            var audioKeys = snapshot?.AudioKeys ?? Array.Empty<AudioKeyInfo>();
            if (snapshot?.SpriteKeyPrefix != _keyPrefix)
            {
                _keyPrefix = snapshot?.SpriteKeyPrefix;
                _scenarioSpriteKeys = null;
                _portraitRows.Clear();
                _resolvedSprites.Clear();
            }
            _spriteLoaderType = snapshot?.SpriteLoaderType ?? "";

            // アセットカタログが無いときはキャプチャ済みキャラが情報源になる (DrawCharacters と同じ優先順)
            var characterCount = _characters.Count == 0
                ? snapshot?.Characters.Count ?? 0
                : _characters.Sum(c => c.Entries.Count);
            var sections = ProjectReferenceSections.All;
            var labels = new List<string>
            {
                $"キャラ ({characterCount})",
                $"画像 ({_imageGroups.Sum(g => g.Keys.Count)})",
                $"構図 ({layouts.Count})",
                $"BGM / SE ({audioKeys.Count})",
                $"コマンド ({CommandTabCount(snapshot)})",
            };
            foreach (var section in sections) labels.Add($"{section.Title} ({section.Count})");
            var index = Mathf.Clamp(GUILayout.Toolbar(_tabIndex, labels.ToArray()), 0, labels.Count - 1);
            if (index != _tabIndex)
            {
                _tabIndex = index;
                _scroll = Vector2.zero;
                GUI.FocusControl(null);
            }

            _scroll = EditorGUILayout.BeginScrollView(_scroll);
            if (_tabIndex >= BuiltinTabCount)
            {
                sections[_tabIndex - BuiltinTabCount].Draw(_rows ??= new Rows(this));
            }
            else
            {
                switch (_tab)
                {
                    case Tab.Characters: DrawCharacters(snapshot); break;
                    case Tab.Images: DrawImages(); break;
                    case Tab.Layouts: DrawLayouts(snapshot, layouts); break;
                    case Tab.Audio: DrawAudio(snapshot, audioKeys); break;
                    case Tab.Commands: DrawCommands(snapshot); break;
                }
            }
            EditorGUILayout.EndScrollView();
        }

        private const int BuiltinTabCount = 5;

        /// <summary>拡張セクション (opt-in アセンブリ) が組込タブと同じ見た目で行を描くための口。</summary>
        public sealed class Rows
        {
            private readonly ProjectReferenceWindow _window;
            internal Rows(ProjectReferenceWindow window) => _window = window;

            /// <summary>検索欄に一致するか。</summary>
            public bool Matches(string text) => _window.Matches(text);

            /// <summary>サムネイル (アセットがあれば) + キーチップ + 補足ラベルの 1 行。クリックで ping。</summary>
            public void DrawKeyRow(string key, string subLabel, string? assetPath) =>
                _window.DrawThumbnailRow(assetPath, key, subLabel, key);

            public void DrawGroupHeader(string text) => EditorGUILayout.LabelField(text, EditorStyles.miniBoldLabel);
            public void DrawNote(string text) => EditorGUILayout.LabelField(text, EditorStyles.miniLabel);
            public void DrawInfo(string text) => EditorGUILayout.HelpBox(text, MessageType.Info);
        }

        private bool Matches(string text) =>
            string.IsNullOrEmpty(_search) || text.IndexOf(_search, StringComparison.OrdinalIgnoreCase) >= 0;

        // スクロール外の行はサムネイル要求を出さない (AssetPreview キャッシュの浪費防止)
        private bool IsRowVisible(Rect rect) =>
            rect.yMax >= _scroll.y - 100f && rect.y <= _scroll.y + position.height + 100f;

        // ---- キャラ (ScriptableCharacterCatalog をライブ表示。無ければ DI ビルド時キャプチャ) ----

        private void DrawCharacters(NovelProjectCapture.Snapshot? snapshot)
        {
            var captured = _characters!.Count == 0 ? snapshot?.Characters : null;
            if (captured is { Count: > 0 })
            {
                EditorGUILayout.LabelField(
                    $"取得元: {snapshot!.CharacterCatalogType} ({FormatTime(snapshot.CapturedAt)} の再生時)", EditorStyles.miniLabel);
                DrawSpriteKeyHeader();
                foreach (var c in captured)
                    DrawCharacterEntry(c.Id, c.DisplayName, c.DefaultPortraitKey ?? "");
                return;
            }
            if (_characters.Count == 0)
            {
                EditorGUILayout.HelpBox(
                    "ScriptableCharacterCatalog が見つかりません (Create > Novel > Character Catalog)。" +
                    "コード実装のカタログは ICharacterCatalog.EnumerateEntries() で目録を返し、一度再生してください。",
                    MessageType.Info);
                return;
            }
            DrawSpriteKeyHeader();
            foreach (var catalog in _characters)
            {
                DrawPingableHeader(catalog.AssetPath, catalog.AssetPath);
                foreach (var (id, displayName, defaultPortrait) in catalog.Entries)
                    DrawCharacterEntry(id, displayName, defaultPortrait);
            }
        }

        /// <summary>キャラ 1 人分。見出し (id + 表示名) に続けて、そのキャラの立ち絵キーを並べる。</summary>
        private void DrawCharacterEntry(string id, string displayName, string defaultPortraitKey)
        {
            var rows = PortraitRowsOf(id, defaultPortraitKey);
            // キャラ自体が検索に当たれば立ち絵は全件見せる。当たらない場合はキー側の一致だけを拾う
            var characterMatches = Matches(id) || Matches(displayName);
            var shown = characterMatches ? rows : rows.Where(r => Matches(r.Key)).ToList();
            if (!characterMatches && shown.Count == 0) return;

            using (new EditorGUILayout.HorizontalScope())
            {
                DrawKeyChip(id, id);
                EditorGUILayout.LabelField($"表示名: {displayName}", EditorStyles.miniLabel);
            }
            if (shown.Count == 0)
            {
                EditorGUILayout.LabelField("    立ち絵が見つかりません (キー名にキャラ id を含めるか、既定立ち絵を設定すると一覧できます)",
                    EditorStyles.miniLabel);
                return;
            }
            foreach (var row in shown)
            {
                var note = row.ShortName == row.Key ? "" : $"短縮: {row.ShortName}";
                if (row.IsDefault) note = string.IsNullOrEmpty(note) ? "既定" : $"{note}    既定";
                DrawThumbnailRow(ResolveSpritePath(row.Key), row.Key, note, row.Key);
            }
        }

        private IReadOnlyList<PortraitKeyRow> PortraitRowsOf(string id, string defaultPortraitKey)
        {
            // 全キー走査をキャラ数 x 再描画回数だけ繰り返さないようキャッシュする (Invalidate で捨てる)
            if (_portraitRows.TryGetValue(id, out var cached)) return cached;
            var rows = PortraitKeyGrouping.Collect(id, defaultPortraitKey, ScenarioSpriteKeys());
            _portraitRows[id] = rows;
            return rows;
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
            DrawSpriteKeyHeader();
            if (_imageGroups!.Count == 0)
            {
                EditorGUILayout.HelpBox("Resources 配下にスプライトが見つかりません。", MessageType.Info);
                return;
            }
            foreach (var group in _imageGroups)
            {
                var rows = group.Keys
                    .Select(resourceKey => (resourceKey, key: ToScenarioKey(resourceKey)))
                    .Where(r => Matches(r.key ?? r.resourceKey))
                    .ToList();
                if (rows.Count == 0) continue;
                EditorGUILayout.LabelField($"{group.Folder} ({rows.Count})", EditorStyles.miniBoldLabel);
                foreach (var (resourceKey, key) in rows)
                    DrawThumbnailRow(_spritePathByKey!.GetValueOrDefault(resourceKey),
                        key ?? resourceKey,
                        key == null ? "ローダの root 外のため、このシナリオからは読めない" : "",
                        key);
            }
        }

        /// <summary>
        /// 表示しているキーが「シナリオにそのまま書ける文字列」かどうかを明示する。
        /// ローダの root が分からない構成では Resources 相対パスのままである旨を断る (誤ったキーを断定しない)。
        /// </summary>
        private void DrawSpriteKeyHeader()
        {
            if (_keyPrefix == null)
            {
                EditorGUILayout.HelpBox(
                    _spriteLoaderType.Length == 0
                        ? "スプライトローダが未キャプチャのため、キーは Resources 相対パスをそのまま表示しています。一度再生すると実際の配線から取得します。"
                        : $"{_spriteLoaderType} が ISpriteKeyPrefix を実装していないため root が分かりません。" +
                          "キーは Resources 相対パスをそのまま表示しています (root を付けるローダではその分ズレます)。",
                    MessageType.Info);
                return;
            }
            var root = _keyPrefix.Length == 0 ? "root なし" : $"root: {_keyPrefix}";
            EditorGUILayout.LabelField(
                $"キー = シナリオにそのまま書ける文字列。取得元: {_spriteLoaderType} ({root})。クリックでアセットを選択。",
                EditorStyles.miniLabel);
        }

        /// <summary>Resources 相対パスを、シナリオにそのまま書けるキーへ変換する (root 外なら null)。</summary>
        private string? ToScenarioKey(string resourceKey)
        {
            if (string.IsNullOrEmpty(_keyPrefix)) return resourceKey;
            return resourceKey.StartsWith(_keyPrefix, StringComparison.Ordinal)
                ? resourceKey.Substring(_keyPrefix!.Length)
                : null;
        }

        private List<string> ScenarioSpriteKeys() => _scenarioSpriteKeys ??= _spritePathByKey!.Keys
            .Select(ToScenarioKey)
            .Where(k => k != null)
            .Select(k => k!)
            .ToList();

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
            // root が判明していれば付け直して一意に引ける。不明なときだけ後方一致の推測に落とす
            var path = !string.IsNullOrEmpty(_keyPrefix) && _spritePathByKey!.TryGetValue(_keyPrefix + key, out var rooted)
                ? rooted
                : ResolveByKey(_spritePathByKey!, key);
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
                    // stage :single と書ける形でコピーする (表示と同じ文字列)
                    DrawKeyChip($":{layout.Id}", $":{layout.Id}", 120f);
                    DrawSlotDiagram(layout.SlotCount);
                    var note = string.IsNullOrEmpty(layout.Note) ? "" : $"  {layout.Note}";
                    EditorGUILayout.LabelField($"{layout.SlotCount} 人{note}");
                }
            }
        }

        /// <summary>そのまま .rb に書ける呼び出し形。語彙登録だけで確実に通るのは cmd + キーワード引数の形のみ
        /// (VitalRouter.MRuby が Object に定義するのは cmd だけで、裸の名前は糖衣が無いと NoMethodError)。</summary>
        private static string CommandTemplate(CommandKeyInfo command)
        {
            if (command.Parameters.Count == 0) return $"cmd :{command.Name}";
            return $"cmd :{command.Name}, {string.Join(", ", command.Parameters.Select(p => $"{p.Name}: {EmptyLiteral(p.TypeName)}"))}";
        }

        private static string EmptyLiteral(string typeName) => typeName switch
        {
            "string" => "''",
            "bool" => "false",
            "int" or "long" => "0",
            "float" or "double" => "0.0",
            _ when typeName.EndsWith("[]", StringComparison.Ordinal) => "[]",
            _ => "nil",
        };

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
                    "音キーは IAudioChannel.EnumerateKeys() から取得します。自前チャンネルで目録を返し、一度再生してください。",
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
            // キャプチャ済みのアセット参照 (EnumerateKeys が渡した AudioClip・GUID 永続化) が最優先。
            // 無ければ Resources 相対パスとしての照合に落とす
            var clip = key.Asset as AudioClip;
            if (clip == null)
            {
                var clipPath = ResolveAudioPath(key.Key);
                clip = clipPath == null ? null : AssetDatabase.LoadAssetAtPath<AudioClip>(clipPath);
            }
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
                    // アセット参照もパス照合も無ければ試聴不可 (自前チャンネルのキー体系は編集モードでは分からない)
                    using (new EditorGUI.DisabledScope(true))
                        GUILayout.Button(new GUIContent("▶",
                            "キーに対応する AudioClip が見つからないため試聴できません。" +
                            "EnumerateKeys() で AudioKeyInfo に AudioClip を渡す (推奨) か、キーを Resources 相対パスに合わせると試聴できます"), GUILayout.Width(28f));
                }

                var duration = clip == null ? "" : $"  {(int)(clip.length / 60)}:{clip.length % 60:00.0}";
                var chipWidth = Mathf.Max(180f, RowLabel.CalcSize(new GUIContent(key.Key)).x + ChipGap + CopyButtonWidth);
                var rect = GUILayoutUtility.GetRect(chipWidth, EditorGUIUtility.singleLineHeight, GUILayout.Width(chipWidth));
                DrawKeyChip(rect, key.Key, key.Key, out var copyRect);
                // コピーボタン上のクリックは ping に取られないようにする
                if (clip != null && Event.current.type == EventType.MouseDown && rect.Contains(Event.current.mousePosition)
                    && !copyRect.Contains(Event.current.mousePosition))
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

        private static int CommandTabCount(NovelProjectCapture.Snapshot? snapshot) =>
            snapshot == null ? 0
                : snapshot.Commands.Count + snapshot.WorldEffectKeys.Count + snapshot.Preambles.Sum(p => p.MethodNames.Count);

        private void DrawCommands(NovelProjectCapture.Snapshot? snapshot)
        {
            if (snapshot == null || CommandTabCount(snapshot) == 0)
            {
                EditorGUILayout.HelpBox(
                    "糖衣 (preamble の def) ・独自コマンド (INovelCommandModule) ・world_effect のキーは再生時に取得します。一度再生してください。",
                    MessageType.Info);
                return;
            }
            EditorGUILayout.LabelField(
                $"{FormatTime(snapshot.CapturedAt)} の再生時に取得。コピーはそのまま .rb に書ける呼び出し形。",
                EditorStyles.miniLabel);

            DrawPreambleSugars(snapshot.Preambles);
            DrawCommandModules(snapshot.Commands);
            DrawWorldEffectKeys(snapshot);
        }

        // ---- 糖衣 (preamble の def)。ソースが特定できれば引数名・既定値・直上コメントを出す ----

        private void DrawPreambleSugars(IReadOnlyList<PreambleInfo> preambles)
        {
            foreach (var preamble in preambles)
            {
                if (preamble.MethodNames.Count == 0) continue;
                var entry = PreambleSourceCatalog.Find(preamble.BytecodeHash);
                var defs = entry == null ? null : RubyDefParser.Parse(entry.Source).ToDictionary(d => d.Name, d => d);
                var names = preamble.MethodNames.Where(Matches).ToList();
                if (names.Count == 0) continue;

                using (new EditorGUILayout.VerticalScope(SectionBox))
                {
                    if (entry != null) DrawPingableHeader($"糖衣  —  {entry.AssetPath} ({names.Count})", entry.AssetPath);
                    else
                    {
                        EditorGUILayout.LabelField($"糖衣  —  {preamble.SourceType} ({names.Count})", EditorStyles.boldLabel);
                        EditorGUILayout.LabelField("ソース .rb が見つからないため名前のみ", EditorStyles.miniLabel);
                    }
                    var row = 0;
                    foreach (var name in names)
                    {
                        var def = defs != null && defs.TryGetValue(name, out var d) ? d : null;
                        using (ZebraRow(row++))
                        {
                            using (new EditorGUILayout.HorizontalScope())
                            {
                                DrawKeyChip(name, def?.CallTemplate() ?? name, 180f);
                                if (def?.Comment != null) EditorGUILayout.LabelField(def.Comment, RowLabel);
                            }
                            if (def != null && def.Params.Count > 0)
                                DrawDetailLine(def.Signature());
                        }
                    }
                }
                EditorGUILayout.Space(6f);
            }
        }

        // ---- 独自コマンド (INovelCommandModule の語彙)。cmd :name, key: value で呼ぶ ----

        private void DrawCommandModules(IReadOnlyList<CommandKeyInfo> commands)
        {
            foreach (var group in commands.GroupBy(c => c.ModuleType).OrderBy(g => g.Key, StringComparer.Ordinal))
            {
                var rows = group.Where(c => Matches(c.Name) || Matches(c.CommandType)).OrderBy(c => c.Name, StringComparer.Ordinal).ToList();
                if (rows.Count == 0) continue;
                using (new EditorGUILayout.VerticalScope(SectionBox))
                {
                    EditorGUILayout.LabelField($"コマンド  —  {group.Key} ({rows.Count})", EditorStyles.boldLabel);
                    var row = 0;
                    foreach (var command in rows)
                    {
                        using (ZebraRow(row++))
                        {
                            using (new EditorGUILayout.HorizontalScope())
                            {
                                DrawKeyChip(command.Name, CommandTemplate(command), 180f);
                                if (command.Description != null) EditorGUILayout.LabelField(command.Description, RowLabel);
                            }
                            if (command.Parameters.Count > 0)
                                DrawDetailLine(string.Join(",   ", command.Parameters.Select(p =>
                                    p.Description == null ? $"{p.Name}: {p.TypeName}" : $"{p.Name}: {p.TypeName} — {p.Description}")));
                        }
                    }
                }
                EditorGUILayout.Space(6f);
            }
        }

        // ---- world_effect のキー (IWorldEffectSink.EnumerateKeys) ----

        private void DrawWorldEffectKeys(NovelProjectCapture.Snapshot snapshot)
        {
            var rows = snapshot.WorldEffectKeys.Where(k => Matches(k.Key)).OrderBy(k => k.Key, StringComparer.Ordinal).ToList();
            if (rows.Count == 0) return;
            using (new EditorGUILayout.VerticalScope(SectionBox))
            {
                EditorGUILayout.LabelField($"world_effect  —  {snapshot.WorldEffectSinkType} ({rows.Count})", EditorStyles.boldLabel);
                var row = 0;
                foreach (var key in rows)
                {
                    using (ZebraRow(row++))
                    {
                        using (new EditorGUILayout.HorizontalScope())
                        {
                            DrawKeyChip($":{key.Key}", $"world_effect :{key.Key}", 180f);
                            if (key.Note != null) EditorGUILayout.LabelField(key.Note, RowLabel);
                        }
                    }
                }
            }
        }

        // ---- コマンドタブの共通描画 (セクション枠・縞背景・詳細行) ----

        private static GUIStyle? _sectionBox;
        private static GUIStyle SectionBox => _sectionBox ??= new GUIStyle(EditorStyles.helpBox) { padding = new RectOffset(8, 8, 6, 6) };

        /// <summary>縞背景つきエントリ。using で閉じる (Scope の rect へ内容より先に背景を描く)。</summary>
        private static EditorGUILayout.VerticalScope ZebraRow(int index)
        {
            var scope = new EditorGUILayout.VerticalScope();
            if (Event.current.type == EventType.Repaint && (index & 1) == 1)
                EditorGUI.DrawRect(scope.rect, new Color(0.5f, 0.5f, 0.5f, 0.08f));
            return scope;
        }

        /// <summary>エントリ 2 行目の補足 (引数など)。チップの文字位置に揃えてインデントする。</summary>
        private static void DrawDetailLine(string text)
        {
            var rect = EditorGUILayout.GetControlRect(false, EditorGUIUtility.singleLineHeight);
            const float indent = 4f + CopyButtonWidth + ChipGap;
            EditorGUI.LabelField(new Rect(rect.x + indent, rect.y, Mathf.Max(0f, rect.width - indent), rect.height),
                text, EditorStyles.miniLabel);
        }

        // ---- 共通描画 ----

        private const float CopyButtonWidth = 22f;
        private const float ChipGap = 4f;

        private static Texture? _copyIcon;
        private static bool _copyIconResolved;

        /// <summary>コピーボタンの絵柄。組み込みアイコンが無いバージョンではテキストへ落とす。</summary>
        private static Texture? CopyIcon
        {
            get
            {
                if (_copyIconResolved) return _copyIcon;
                _copyIconResolved = true;
                _copyIcon = EditorGUIUtility.IconContent("Clipboard")?.image;
                return _copyIcon;
            }
        }

        /// <summary>キーをクリップボードへ入れるボタン。コピーできるキーが無い行では無効化する。</summary>
        private void DrawCopyButton(Rect rect, string? key)
        {
            var empty = string.IsNullOrEmpty(key);
            var tooltip = empty ? "コピーできるキーがありません" : $"「{key}」をクリップボードへコピー";
            using (new EditorGUI.DisabledScope(empty))
            {
                var content = CopyIcon != null ? new GUIContent(CopyIcon, tooltip) : new GUIContent("C", tooltip);
                if (GUI.Button(rect, content, EditorStyles.miniButton))
                {
                    EditorGUIUtility.systemCopyBuffer = key;
                    ShowNotification(new GUIContent($"コピーしました\n{key}"));
                }
            }
        }

        /// <summary>
        /// コピーボタンとキー文字列を隣接させた 1 つのまとまり。行の右端にボタンを寄せると
        /// 読む位置と押す位置が離れて使いづらいため、キーのすぐ横に置く。
        /// ボタンを文字列の左に置くのは、キーの長さで押す位置がずれず縦に揃うため。
        /// </summary>
        /// <returns>チップが消費した幅 (後続のラベルを続けて置くのに使う)。</returns>
        private float DrawKeyChip(Rect rect, string label, string? copyKey, out Rect copyRect)
        {
            copyRect = new Rect(rect.x, rect.y + (rect.height - EditorGUIUtility.singleLineHeight) / 2f,
                CopyButtonWidth, EditorGUIUtility.singleLineHeight);
            DrawCopyButton(copyRect, copyKey);
            var textWidth = Mathf.Min(RowLabel.CalcSize(new GUIContent(label)).x,
                Mathf.Max(0f, rect.width - CopyButtonWidth - ChipGap));
            GUI.Label(new Rect(copyRect.xMax + ChipGap, rect.y, textWidth, rect.height), label, RowLabel);
            return CopyButtonWidth + ChipGap + textWidth;
        }

        /// <summary>横並びレイアウト中に置くキーチップ (<paramref name="minWidth"/> で列を揃えられる)。</summary>
        private void DrawKeyChip(string label, string? copyKey, float minWidth = 0f)
        {
            // GUILayoutUtility.GetRect は EditorGUILayout のような左余白を取らず、行頭で枠に張り付く
            const float leftPad = 4f;
            var width = leftPad + Mathf.Max(minWidth, RowLabel.CalcSize(new GUIContent(label)).x + ChipGap + CopyButtonWidth);
            var rect = GUILayoutUtility.GetRect(width, EditorGUIUtility.singleLineHeight, GUILayout.Width(width));
            DrawKeyChip(new Rect(rect.x + leftPad, rect.y, rect.width - leftPad, rect.height), label, copyKey, out _);
        }

        /// <summary>サムネイル付きの 1 行。クリックでアセットを ping する。</summary>
        private void DrawThumbnailRow(string? assetPath, string label, string subLabel, string? copyKey = null)
        {
            var size = Mathf.Round(_thumbSize);
            var rect = EditorGUILayout.GetControlRect(false, size);
            var thumbRect = new Rect(rect.x, rect.y + 1f, size, size - 2f);
            var contentRect = new Rect(thumbRect.xMax + 6f, rect.y, Mathf.Max(0f, rect.width - size - 6f), rect.height);

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

            var used = DrawKeyChip(contentRect, label, copyKey, out var copyRect);
            if (!string.IsNullOrEmpty(subLabel))
                GUI.Label(new Rect(contentRect.x + used + 8f, rect.y,
                    Mathf.Max(0f, contentRect.width - used - 8f), rect.height), subLabel, RowLabel);

            // コピーボタン上のクリックは ping に取られないようにする
            if (assetPath != null && Event.current.type == EventType.MouseDown && rect.Contains(Event.current.mousePosition)
                && !copyRect.Contains(Event.current.mousePosition))
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
                var args = PlayMethod?.GetParameters().Length switch
                {
                    1 => new object[] { clip },
                    2 => new object[] { clip, 0 },
                    3 => new object[] { clip, 0, false },
                    _ => null,
                };
                if (args == null)
                {
                    // 無言の no-op だと「押しても鳴らない」の原因が分からないため理由を出す
                    Debug.LogWarning("[Novel] UnityEditor.AudioUtil の再生 API が見つからないため試聴できません (Unity バージョン差異の可能性)。");
                    return;
                }
                PlayMethod!.Invoke(null, args);
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
