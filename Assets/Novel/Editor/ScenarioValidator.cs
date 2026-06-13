#nullable enable
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Novel.Editor
{
    // 全 .rb シナリオが .mrb バイトコード（mrubycs-compiler のサブアセット）を生成できているか検証する。
    // 構文エラー等でコンパイルに失敗した .rb を編集時に洗い出す用途
    public static class ScenarioValidator
    {
        [MenuItem("Novel/Validate Scenarios")]
        public static void Validate()
        {
            int total = 0;
            int failed = 0;

            foreach (var guid in AssetDatabase.FindAssets("t:TextAsset"))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                if (!path.EndsWith(".rb")) continue;

                total++;
                var hasBytecode = AssetDatabase.LoadAllAssetsAtPath(path)
                    .Any(a => a != null && a.name.EndsWith(".mrb"));
                if (!hasBytecode)
                {
                    failed++;
                    Debug.LogError($"[Novel] バイトコード未生成（コンパイル失敗の可能性）: {path}");
                }
            }

            Debug.Log($"[Novel] シナリオ検証完了: {total} 件中 {failed} 件が未コンパイル");
        }
    }
}
