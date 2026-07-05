#nullable enable
using System;
using UnityEngine;

namespace Novel.Runtime
{
    // 永続状態(NovelStateSnapshot)を JSON 文字列として出し入れする。
    // 主用途: ゲームが自前セーブ機構を持ち「文字列で受け取って自分で保存したい」ケース。
    //   保存: var json = NovelSaveSerializer.Serialize(runner.CaptureState()); を自分の save に書く。
    //   復元: NovelSaveSerializer.TryDeserialize(json, out var snap); runner.RestoreState(snap);（PlayAsync 前）。
    // 実際の永続化(ファイル/PlayerPrefs/クラウド)はゲーム側の責務。
    //
    // 文字列でなくオブジェクトとして自前 serde にネストしたい場合は NovelSaveData(公開クラス)を直接使う。
    // 直列化は Unity 標準の JsonUtility(com.unity.modules.jsonserialize・追加パッケージ不要)。
    // 出力形式(決定的。キー/既読 id を序数ソート):
    //   {"version":1,"values":[{"key":"coins","value":30}],"read":["a1b2c3d4e5f60718"]}
    public static class NovelSaveSerializer
    {
        // 保存形式のバージョン。将来スキーマを変える際の移行フック(現状は読み取るだけ)。
        public const int FormatVersion = 1;

        // 空(セーブ未作成/破損時のフォールバック)の snapshot。
        public static NovelStateSnapshot Empty
            => new(new System.Collections.Generic.Dictionary<string, int>(), Array.Empty<string>());

        public static string Serialize(NovelStateSnapshot snapshot)
            => JsonUtility.ToJson(NovelSaveData.From(snapshot));

        // 破損/未作成に強い読み取り。null/空/不正な JSON は false + Empty を返す(セーブ破損 → 新規開始)。
        public static bool TryDeserialize(string? json, out NovelStateSnapshot snapshot)
        {
            if (string.IsNullOrWhiteSpace(json))
            {
                snapshot = Empty;
                return false;
            }

            try
            {
                snapshot = Deserialize(json!);
                return true;
            }
            catch (NovelSaveFormatException)
            {
                snapshot = Empty;
                return false;
            }
        }

        // 厳密読み取り。不正な JSON / 非オブジェクトは NovelSaveFormatException。
        // 未知フィールドは無視、"values"/"read" 欠落は空として許容する(前方/後方互換)。
        public static NovelStateSnapshot Deserialize(string json)
        {
            NovelSaveData? data;
            try
            {
                data = JsonUtility.FromJson<NovelSaveData>(json);
            }
            catch (Exception e)
            {
                // JsonUtility は不正 JSON / 非オブジェクトルートで ArgumentException 等を投げる
                throw new NovelSaveFormatException($"invalid save JSON: {e.Message}");
            }

            if (data == null) throw new NovelSaveFormatException("save JSON deserialized to null");
            return data.ToSnapshot();
        }
    }

    // セーブ JSON が壊れている/形式不正のときに投げる。TryDeserialize はこれを握って Empty を返す。
    public sealed class NovelSaveFormatException : Exception
    {
        public NovelSaveFormatException(string message) : base(message) { }
    }
}
