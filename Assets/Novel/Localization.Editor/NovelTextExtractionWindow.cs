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
        // EditorPrefs だと別プロジェクトのスキャンルートを持ち越して誤設定ガードに掛かるため、プロジェクト単位で保存する
        private const string TableNamePrefsKey = "Novel.Localization.TableCollectionName";
        private const string ScanRootPrefsKey = "Novel.Localization.ScanRoot";

        private string _tableName = "NovelText";
        private string _scanRoot = "";
        private ExtractionPlan? _plan;
        private Vector2 _scroll;
        private bool _confirmedRiskyApply;   // 走査 0 件で全消滅になる計画を人間が明示承認したか

        [MenuItem("Novel/Localization/Extract Strings...")]
        private static void Open()
        {
            var window = GetWindow<NovelTextExtractionWindow>("Novel Text Extraction");
            window._tableName = EditorUserSettings.GetConfigValue(TableNamePrefsKey) ?? "NovelText";
            window._scanRoot = EditorUserSettings.GetConfigValue(ScanRootPrefsKey) ?? "";
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
                EditorUserSettings.SetConfigValue(TableNamePrefsKey, tableName);
                _plan = null;   // 別テーブル基準の古い計画を適用させない (Scan からやり直し)
                _confirmedRiskyApply = false;
            }
            var scanRoot = EditorGUILayout.TextField(
                new GUIContent("スキャンルート", "Assets からの相対パス。空 = Assets 全体。~/Tests/Editor フォルダは常に除外"), _scanRoot);
            if (scanRoot != _scanRoot)
            {
                _scanRoot = scanRoot;
                EditorUserSettings.SetConfigValue(ScanRootPrefsKey, scanRoot);
                _plan = null;   // 走査範囲が変わった計画も無効化
                _confirmedRiskyApply = false;
            }

            EditorGUILayout.Space();
            if (GUILayout.Button("Scan（計画を作成。テーブルはまだ変更しない）"))
                ScanProject();

            if (_plan == null) return;
            DrawPlan(_plan);

            EditorGUILayout.Space();
            // 走査 0 件なのに消滅が出るのは、ほぼスキャンルートの指定ミス。適用すると全エントリが
            // deprecated + 追跡メタデータ消去になるため、確認してからでないと押せなくする
            // カタログ表示名は .rb 走査が 0 件でも載るため、シナリオ由来が 0 件かどうかで判定する
            var scenarioTextCount = _plan.CurrentPerFile
                .Where(pair => pair.Key != CatalogTextCollector.SourceKey)
                .Sum(pair => pair.Value.Count);
            var looksLikeMisconfiguration = scenarioTextCount == 0 && _plan.Deprecations.Count > 0;
            if (looksLikeMisconfiguration)
            {
                EditorGUILayout.HelpBox(
                    $"走査結果が 0 件なのに既存エントリ {_plan.Deprecations.Count} 件が消滅扱いになります。\n" +
                    "スキャンルートの指定ミスの可能性が高いので、意図的でなければ適用しないでください。",
                    MessageType.Error);
                _confirmedRiskyApply = EditorGUILayout.ToggleLeft(
                    "全シナリオを削除したので、この結果で正しい", _confirmedRiskyApply);
            }

            // 3 リストが空でも「出所メタデータだけ変わる」変更 (共有原文の出現が 1 つ減った等) は起きる。
            // Apply の RebuildSourceMetadata がそれを書き戻さないと、次回 diff が古い出現数を基準にしてしまう。
            // よって走査が完了していれば常に適用できるようにし、危険な計画のガードだけを残す
            var canApply = !looksLikeMisconfiguration || _confirmedRiskyApply;
            using (new EditorGUI.DisabledScope(!canApply))
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
                        _confirmedRiskyApply = false;
                    }
                }
            }
        }

        private void ScanProject()
        {
            var collection = GetCollection();
            if (collection == null) return;
            var plan = ExtractionPlanner.Scan(Application.dataPath, _scanRoot);
            // .rb に出ないが画面に出るテキスト (キャラカタログの表示名) も同じ計画に載せる
            CatalogTextCollector.AddTo(plan);
            ExtractionPlanner.BuildDiff(plan, new UnityLocalizationTableEditor(collection));
            _plan = plan;
            _confirmedRiskyApply = false;
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
            // Additions は planner が重複と既存キーを畳んだ「新規起票する原文」そのもの
            var additions = plan.Additions;
            EditorGUILayout.LabelField(
                $"走査: {plan.CurrentPerFile.Count} ファイル / {total} 件 — " +
                $"新規 {additions.Count}・リネーム/分離 {plan.Renames.Count}・deprecated {plan.Deprecations.Count}",
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

            if (additions.Count > 0)
            {
                EditorGUILayout.LabelField($"新規（未訳で起票）: {additions.Count} 件", EditorStyles.boldLabel);
                foreach (var text in additions.Take(200))
                    EditorGUILayout.LabelField("  " + Truncate(text));
                if (additions.Count > 200)
                    EditorGUILayout.LabelField($"  … 他 {additions.Count - 200} 件");
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
