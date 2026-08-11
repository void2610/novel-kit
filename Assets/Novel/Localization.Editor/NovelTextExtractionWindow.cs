#nullable enable
using System.Linq;
using System.Text;
using Novel.Editor.Localization;
using UnityEditor;
using UnityEditor.Localization;
using UnityEngine;

namespace Novel.Localization.Editor
{
    /// <summary>
    /// 追跡抽出の移行レポートウィンドウ（localization-unity-package ADR）。
    /// Scan で計画（新規 / リネーム(類似度%) / 分離 / deprecated / 抽出できなかった箇所）を提示し、
    /// 人間が確認してから Apply でテーブルへ書き込む。誤対応はここで気付いて中止できる。
    /// </summary>
    public sealed class NovelTextExtractionWindow : EditorWindow
    {
        private const string TableNamePrefsKey = "Novel.Localization.TableCollectionName";
        private const string ScanRootPrefsKey = "Novel.Localization.ScanRoot";

        private string _tableName = "NovelText";
        private string _scanRoot = "";
        private ExtractionPlan? _plan;
        private Vector2 _scroll;

        [MenuItem("Novel/Localization/Extract Strings...")]
        private static void Open()
        {
            var window = GetWindow<NovelTextExtractionWindow>("Novel Text Extraction");
            window._tableName = EditorPrefs.GetString(TableNamePrefsKey, "NovelText");
            window._scanRoot = EditorPrefs.GetString(ScanRootPrefsKey, "");
        }

        // dev プレイで収集した抽出漏れ (テーブルミス) の一覧をログへ出す
        [MenuItem("Novel/Localization/Report Missing Texts")]
        private static void ReportMissing()
        {
            var missed = MissingTextCollector.Snapshot();
            if (missed.Count == 0)
            {
                Debug.Log("[Novel] 未ヒット原文はありません (収集は dev プレイ中に resolver.TextMissed += MissingTextCollector.Record で配線)");
                return;
            }
            var sb = new StringBuilder($"[Novel] 未ヒット原文 {missed.Count} 件 (静的抽出から漏れた候補):\n");
            foreach (var text in missed) sb.AppendLine(text);
            Debug.Log(sb.ToString());
        }

        private void OnGUI()
        {
            EditorGUILayout.LabelField("String Table Collection", EditorStyles.boldLabel);
            var tableName = EditorGUILayout.TextField("Collection 名", _tableName);
            if (tableName != _tableName)
            {
                _tableName = tableName;
                EditorPrefs.SetString(TableNamePrefsKey, tableName);
                _plan = null;   // 別テーブル基準の古い計画を適用させない (Scan からやり直し)
            }
            var scanRoot = EditorGUILayout.TextField(
                new GUIContent("スキャンルート", "Assets からの相対パス。空 = Assets 全体。~/Tests/Editor フォルダは常に除外"), _scanRoot);
            if (scanRoot != _scanRoot)
            {
                _scanRoot = scanRoot;
                EditorPrefs.SetString(ScanRootPrefsKey, scanRoot);
                _plan = null;   // 走査範囲が変わった計画も無効化
            }

            EditorGUILayout.Space();
            if (GUILayout.Button("Scan（計画を作成。テーブルはまだ変更しない）"))
                ScanProject();

            if (_plan == null) return;
            DrawPlan(_plan);

            EditorGUILayout.Space();
            var hasWork = _plan.Renames.Count + _plan.Additions.Count + _plan.Deprecations.Count > 0;
            using (new EditorGUI.DisabledScope(!hasWork))
            {
                if (GUILayout.Button("Apply（テーブルへ書き込む）") &&
                    EditorUtility.DisplayDialog("Novel Text Extraction",
                        "計画をテーブルへ適用します。よろしいですか？（消滅キーは削除されず deprecated マークになります）",
                        "適用", "キャンセル"))
                {
                    var collection = GetCollection();
                    if (collection != null)
                    {
                        ExtractionApplier.Apply(_plan, new UnityLocalizationTableEditor(collection));
                        Debug.Log($"[Novel] 抽出を適用しました: リネーム/分離 {_plan.Renames.Count}・新規 {_plan.Additions.Count}・deprecated {_plan.Deprecations.Count}");
                        _plan = null;   // 適用済み計画の再適用を防ぐ
                    }
                }
            }
        }

        private void ScanProject()
        {
            var collection = GetCollection();
            if (collection == null) return;
            var plan = ExtractionPlanner.Scan(Application.dataPath, _scanRoot);
            ExtractionPlanner.BuildDiff(plan, new UnityLocalizationTableEditor(collection));
            _plan = plan;
        }

        private StringTableCollection? GetCollection()
        {
            var collection = LocalizationEditorSettings.GetStringTableCollection(_tableName);
            if (collection == null)
                EditorUtility.DisplayDialog("Novel Text Extraction",
                    $"String Table Collection '{_tableName}' が見つかりません。\n" +
                    "Window > Asset Management > Localization Tables で先に作成してください。", "OK");
            return collection;
        }

        private void DrawPlan(ExtractionPlan plan)
        {
            EditorGUILayout.Space();
            var total = plan.CurrentPerFile.Values.Sum(t => t.Count);
            EditorGUILayout.LabelField(
                $"走査: {plan.CurrentPerFile.Count} ファイル / {total} 件 — " +
                $"新規 {plan.Additions.Count}・リネーム/分離 {plan.Renames.Count}・deprecated {plan.Deprecations.Count}",
                EditorStyles.boldLabel);

            _scroll = EditorGUILayout.BeginScrollView(_scroll);

            if (plan.Renames.Count > 0)
            {
                EditorGUILayout.LabelField("原文変更（訳を追従させる）", EditorStyles.boldLabel);
                foreach (var rename in plan.Renames)
                {
                    var label = rename.Kind switch
                    {
                        RenameKind.TagOnly => "タグのみ・訳保持",
                        RenameKind.Fuzzy => $"類似 {rename.Similarity:P0}・訳保持+fuzzy",
                        _ => $"類似 {rename.Similarity:P0}・リライト → 旧訳退避・要再翻訳",
                    };
                    if (rename.IsSplit) label = "分離（旧原文は他所で使用中）・" + label;
                    EditorGUILayout.HelpBox($"[{label}] {rename.File}\n{Truncate(rename.OldText)}\n→ {Truncate(rename.NewText)}", MessageType.Info);
                }
            }

            if (plan.Additions.Count > 0)
            {
                EditorGUILayout.LabelField($"新規（未訳で起票）: {plan.Additions.Count} 件", EditorStyles.boldLabel);
                foreach (var text in plan.Additions.Take(200))
                    EditorGUILayout.LabelField("  " + Truncate(text));
                if (plan.Additions.Count > 200)
                    EditorGUILayout.LabelField($"  … 他 {plan.Additions.Count - 200} 件");
            }

            if (plan.Deprecations.Count > 0)
            {
                EditorGUILayout.LabelField("消滅（削除せず deprecated マーク）", EditorStyles.boldLabel);
                foreach (var text in plan.Deprecations)
                    EditorGUILayout.LabelField("  " + Truncate(text));
            }

            if (plan.Issues.Count > 0)
            {
                EditorGUILayout.LabelField("抽出できなかった箇所", EditorStyles.boldLabel);
                foreach (var issue in plan.Issues)
                    EditorGUILayout.HelpBox(issue, MessageType.Warning);
            }

            EditorGUILayout.EndScrollView();
        }

        private static string Truncate(string text)
            => text.Length <= 60 ? text : text.Substring(0, 60) + "…";
    }
}
