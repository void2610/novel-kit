#nullable enable
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Cysharp.Threading.Tasks;
using Novel.Assets;
using UnityEditor;
using UnityEngine;

namespace Novel.Editor
{
    /// <summary>
    /// 開いているシーンの <see cref="IPortraitChannel"/> を編集モードのまま駆動し、構図を実物の立ち絵で確認する窓。
    /// slot 座標は再生して初めて適用されるため、これが無いと調整のたびに Play を往復することになる。
    /// </summary>
    public sealed class StagePreviewWindow : EditorWindow
    {
        private const string SPRITE_PREFS_PREFIX = "Novel.StagePreview.Sprite.";
        private const string LAYOUT_PREFS_KEY = "Novel.StagePreview.Layout";

        private MonoBehaviour? _channelBehaviour;
        private StageLayoutInfo[] _layouts = System.Array.Empty<StageLayoutInfo>();
        private int _layoutIndex;
        private readonly List<Sprite?> _sprites = new();
        private readonly List<Vector2> _positions = new();
        private string _loadedLayoutId = "";
        private string? _warning;

        [MenuItem("Novel/Stage Preview")]
        private static void Open() => GetWindow<StagePreviewWindow>("Stage Preview").Show();

        private void OnEnable()
        {
            Detect();
            _layoutIndex = EditorPrefs.GetInt(LAYOUT_PREFS_KEY, 0);
        }

        private void OnGUI()
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField("対象", GUILayout.Width(40f));
                var assigned = (MonoBehaviour?)EditorGUILayout.ObjectField(_channelBehaviour, typeof(MonoBehaviour), true);
                // 差し替えた実装が別の構図を持つため、対象が変わったら目録を取り直す
                if (!ReferenceEquals(assigned, _channelBehaviour))
                {
                    _channelBehaviour = assigned;
                    RefreshLayouts();
                }
                if (GUILayout.Button("再検出", GUILayout.Width(60f))) Detect();
            }

            if (_channelBehaviour is not IPortraitChannel channel)
            {
                EditorGUILayout.HelpBox("シーン内に IPortraitChannel の実装が見つかりません。シーンを開くか、対象を直接指定してください。", MessageType.Info);
                return;
            }

            if (_layouts.Length == 0)
            {
                EditorGUILayout.HelpBox("この実装は構図を 1 つも返していません (EnumerateLayouts)。", MessageType.Warning);
                return;
            }

            EditorGUILayout.Space();
            _layoutIndex = EditorGUILayout.Popup("構図", Mathf.Clamp(_layoutIndex, 0, _layouts.Length - 1), _layouts.Select(l => $"{l.Id} ({l.SlotCount}人)").ToArray());
            var layout = _layouts[_layoutIndex];

            SyncSpriteCount(layout.SlotCount);
            var editor = _channelBehaviour as IStageLayoutEditor;
            if (editor != null) SyncPositions(editor, layout);

            EditorGUILayout.Space();
            for (var i = 0; i < _sprites.Count; i++)
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    _sprites[i] = (Sprite?)EditorGUILayout.ObjectField($"slot {i}", _sprites[i], typeof(Sprite), false);
                    if (editor == null) continue;

                    EditorGUI.BeginChangeCheck();
                    var moved = EditorGUILayout.Vector2Field(GUIContent.none, _positions[i], GUILayout.Width(140f));
                    // 数値をいじった瞬間に見えないと、結局 適用 を押す往復が残る
                    if (EditorGUI.EndChangeCheck())
                    {
                        _positions[i] = moved;
                        Store(editor, layout, "Edit stage layout");
                        Apply(channel, layout);
                    }
                }
            }

            EditorGUILayout.Space();
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("適用")) Apply(channel, layout);
                if (GUILayout.Button("クリア")) Clear(channel, layout);
            }

            if (editor != null && GUILayout.Button("シーンの位置を構図へ取り込む"))
            {
                _positions.Clear();
                _positions.AddRange(editor.GetCurrentSlotPositions().Take(layout.SlotCount));
                Store(editor, layout, "Capture stage layout");
            }

            if (!string.IsNullOrEmpty(_warning)) EditorGUILayout.HelpBox(_warning, MessageType.Warning);

            if (editor == null)
                EditorGUILayout.HelpBox("この実装は IStageLayoutEditor を実装していないため、座標はプレビューのみです。窓から編集するには実装してください。", MessageType.Info);

            EditorGUILayout.HelpBox("プレビューの表示状態はシーンを直接書き換えます (座標は再生時に構図から再適用されるため、保存せず閉じても問題ありません)。座標の変更は構図に保存されるので、プレハブ上のインスタンスなら Overrides の適用が要ります。", MessageType.None);
        }

        private void Detect()
        {
            _channelBehaviour = Object.FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                .FirstOrDefault(b => b is IPortraitChannel);
            RefreshLayouts();
        }

        private void RefreshLayouts()
        {
            _layouts = _channelBehaviour is IPortraitChannel c ? c.EnumerateLayouts().ToArray() : System.Array.Empty<StageLayoutInfo>();
        }

        // 構図を切り替えたら、その構図に保存されている座標を読み直す
        private void SyncPositions(IStageLayoutEditor editor, StageLayoutInfo layout)
        {
            if (_loadedLayoutId == layout.Id && _positions.Count == layout.SlotCount) return;

            _loadedLayoutId = layout.Id;
            _positions.Clear();
            _positions.AddRange(editor.GetLayoutPositions(layout.Id));
            while (_positions.Count < layout.SlotCount) _positions.Add(Vector2.zero);
            while (_positions.Count > layout.SlotCount) _positions.RemoveAt(_positions.Count - 1);
        }

        private void Store(IStageLayoutEditor editor, StageLayoutInfo layout, string undoName)
        {
            Undo.RecordObject(_channelBehaviour, undoName);
            editor.SetLayoutPositions(layout.Id, _positions);
            EditorUtility.SetDirty(_channelBehaviour!);
        }

        private void SyncSpriteCount(int slotCount)
        {
            while (_sprites.Count < slotCount) _sprites.Add(LoadPref(_sprites.Count));
            while (_sprites.Count > slotCount) _sprites.RemoveAt(_sprites.Count - 1);
        }

        private void Apply(IPortraitChannel channel, StageLayoutInfo layout)
        {
            _warning = null;
            EditorPrefs.SetInt(LAYOUT_PREFS_KEY, _layoutIndex);

            var pending = !TryRunToCompletion(channel.SwitchLayoutAsync(new PortraitLayout(layout.Id), CancellationToken.None));

            for (var i = 0; i < _sprites.Count; i++)
            {
                SavePref(i, _sprites[i]);
                if (_sprites[i] == null)
                {
                    // 前回の表示を引きずると、指定していない slot が古い絵のまま残る
                    if (!TryRunToCompletion(channel.HideAsync(i, CancellationToken.None))) pending = true;
                    continue;
                }

                var key = AssetDatabase.GetAssetPath(_sprites[i]);
                if (!TryRunToCompletion(channel.ShowAsync(i, new ResolvedSprite(key, _sprites[i]), CancellationToken.None))) pending = true;
            }

            if (pending) _warning = "実装の処理が編集モードで完了しませんでした。アニメーションを待つ実装は、再生中でないときは即座に反映するようにしてください。";
            Repaint();
            SceneView.RepaintAll();
        }

        private void Clear(IPortraitChannel channel, StageLayoutInfo layout)
        {
            _warning = null;
            for (var i = 0; i < layout.SlotCount; i++) TryRunToCompletion(channel.HideAsync(i, CancellationToken.None));
            SceneView.RepaintAll();
        }

        // 編集モードでは PlayerLoop が回らないため、その場で完了した処理だけを結果とみなす
        private static bool TryRunToCompletion(UniTask task)
        {
            var awaiter = task.GetAwaiter();
            if (!awaiter.IsCompleted) return false;

            awaiter.GetResult();
            return true;
        }

        private static Sprite? LoadPref(int index)
        {
            var guid = EditorPrefs.GetString(SPRITE_PREFS_PREFIX + index, "");
            if (string.IsNullOrEmpty(guid)) return null;
            return AssetDatabase.LoadAssetAtPath<Sprite>(AssetDatabase.GUIDToAssetPath(guid));
        }

        private static void SavePref(int index, Sprite? sprite)
        {
            var path = sprite == null ? "" : AssetDatabase.GetAssetPath(sprite);
            EditorPrefs.SetString(SPRITE_PREFS_PREFIX + index, string.IsNullOrEmpty(path) ? "" : AssetDatabase.AssetPathToGUID(path));
        }
    }
}
