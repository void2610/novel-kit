#nullable enable
using System;
using System.Collections.Generic;

namespace Novel.Runtime
{
    // novel-kit の永続状態(フラグ/変数 + 既読)をシリアライズ可能なプレーンクラスとして公開する。
    // 主用途: ゲームが「自前の JSON セーブ機構」を持っているケース。ゲームは NovelSaveData.From(snapshot)
    // を受け取り、自分の save オブジェクトのフィールドとしてそのまま自前 serde(JsonUtility/Newtonsoft/
    // System.Text.Json 等)で直列化する。読み込み時は data.ToSnapshot() で snapshot に戻す。
    //
    // 「文字列でよい」場合は NovelSaveSerializer.Serialize/Deserialize がこのクラス経由で JSON 文字列を出す。
    // フィールドは JsonUtility 互換(Dictionary を使わずキー/値ペアのリスト)にしてある。
    [Serializable]
    public sealed class NovelSaveData
    {
        public int version = NovelSaveSerializer.FormatVersion;
        public List<NovelSaveValue> values = new();
        public List<string> read = new();

        // 出力は決定的(キー/既読 id を序数ソート)にして diff/テストを安定させる。
        public static NovelSaveData From(NovelStateSnapshot snapshot)
        {
            var data = new NovelSaveData();

            var keys = new List<string>(snapshot.Values.Keys);
            keys.Sort(StringComparer.Ordinal);
            foreach (var k in keys)
                data.values.Add(new NovelSaveValue { key = k, value = snapshot.Values[k] });

            data.read = new List<string>(snapshot.ReadTextIds);
            data.read.Sort(StringComparer.Ordinal);
            return data;
        }

        public NovelStateSnapshot ToSnapshot()
        {
            var dict = new Dictionary<string, int>();
            if (values != null)
                foreach (var entry in values)
                    if (!string.IsNullOrEmpty(entry.key))
                        dict[entry.key] = entry.value;

            return new NovelStateSnapshot(dict, read ?? new List<string>());
        }
    }

    [Serializable]
    public struct NovelSaveValue
    {
        public string key;
        public int value;
    }
}
