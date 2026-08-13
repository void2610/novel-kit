#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using Novel.Runtime;
using Novel.View;
using UnityEditor;

namespace Novel.Editor.Localization
{
    /// <summary>
    /// `.rb` に現れないが画面に出るテキストの抽出源（localization-unity-package ADR）。
    ///
    /// 現状の対象は **キャラクターカタログの表示名**。話者名はカタログ（SO / コード実装）にあり
    /// シナリオ本文には出てこないが、ランタイムでは `ICharacterCatalog` → `ITextResolver` を通って
    /// 表示されるため、テーブルに載っていないと日本語のまま残る。
    ///
    /// 収集元は `Novel/Project Reference` / `Validate Scenarios` と同じ和集合:
    /// - `ScriptableCharacterCatalog` アセット（プロジェクト内の全件）
    /// - DI ビルド時キャプチャ（コード実装のカタログ用）
    ///
    /// 疑似ファイル 1 つとして計画へ載せるので、追跡・差分・deprecated の仕組みがそのまま効く
    /// （キャラの改名は「リネーム」として検出され、訳が追従する）。
    /// </summary>
    public static class CatalogTextCollector
    {
        // 出所メタデータに載る疑似ファイル名。`.rb` の相対パスと衝突しない形にしておく
        public const string SourceKey = "<character-catalog>";

        public static void AddTo(ExtractionPlan plan)
        {
            var displayNames = Collect();
            if (displayNames.Count > 0) plan.CurrentPerFile[SourceKey] = displayNames;
        }

        // id 昇順で表示名を返す（出現順が追跡の基準になるため、収集順に依存させない）
        public static List<string> Collect()
        {
            var byId = new SortedDictionary<string, string>(StringComparer.Ordinal);

            foreach (var guid in AssetDatabase.FindAssets("t:ScriptableCharacterCatalog"))
            {
                var asset = AssetDatabase.LoadAssetAtPath<ScriptableCharacterCatalog>(AssetDatabase.GUIDToAssetPath(guid));
                if (asset == null) continue;
                foreach (var entry in asset.EnumerateEntries()) Put(byId, entry);
            }

            // コード実装のカタログはアセットが無いため、DI ビルド時キャプチャから拾う
            var capture = ProjectReferenceCaptureStore.LoadOrLatest();
            if (capture != null)
                foreach (var entry in capture.Characters) Put(byId, entry);

            return byId.Values.ToList();
        }

        // 表示名が空 / id と同じものは翻訳対象にしない（id そのままの表示は未設定と同義で、
        // 訳を当てたい場合はカタログに表示名を書くのが筋）
        private static void Put(SortedDictionary<string, string> byId, CharacterKeyInfo entry)
        {
            if (string.IsNullOrEmpty(entry.Id) || string.IsNullOrEmpty(entry.DisplayName)) return;
            if (entry.DisplayName == entry.Id) return;
            byId[entry.Id] = entry.DisplayName;
        }
    }
}
