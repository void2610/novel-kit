#nullable enable
using System;
using System.Collections.Generic;
using UnityEngine;

namespace Novel.Runtime
{
    // NovelStateSnapshot(フラグ/変数 + 既読)の JSON 直列化をライブラリ側で所有する。
    // color-recollection では ScenarioFlags(Dictionary<string,int>) と read-flags が別々に game 側で
    // シリアライズされていたが、novel-kit は単一 IStateStore に統合済み(state-model ADR)なので、
    // その Capture 結果 = NovelStateSnapshot を 1 つの JSON にまとめて持つ。
    //
    // 直列化は Unity 標準の UnityEngine.JsonUtility(com.unity.modules.jsonserialize)を使う。
    // JsonUtility は Dictionary を直接扱えないため、キー/値ペアの DTO を挟む。
    // 出力は決定的(キー/既読 id を序数ソート)で diff/テストが安定する:
    //   {"version":1,"values":[{"key":"coins","value":30}],"read":["a1b2c3d4e5f60718"]}
    //
    // IO は持たず string ⇔ snapshot のみ。永続先(PlayerPrefs/File)は INovelSaveBlobStore に委譲する
    // (JsonSaveStore が両者を束ねる)。
    public static class NovelSaveSerializer
    {
        // 保存形式のバージョン。将来スキーマを変える際の移行フック(現状は読み取るだけ)。
        public const int FormatVersion = 1;

        // 空(セーブ未作成/破損時のフォールバック)の snapshot。
        public static NovelStateSnapshot Empty
            => new(new Dictionary<string, int>(), Array.Empty<string>());

        [Serializable]
        private struct ValueEntry
        {
            public string key;
            public int value;
        }

        [Serializable]
        private struct SaveDto
        {
            public int version;
            public List<ValueEntry> values;
            public List<string> read;
        }

        public static string Serialize(NovelStateSnapshot snapshot)
        {
            var keys = new List<string>(snapshot.Values.Keys);
            keys.Sort(StringComparer.Ordinal);
            var values = new List<ValueEntry>(keys.Count);
            foreach (var k in keys)
                values.Add(new ValueEntry { key = k, value = snapshot.Values[k] });

            var read = new List<string>(snapshot.ReadTextIds);
            read.Sort(StringComparer.Ordinal);

            var dto = new SaveDto { version = FormatVersion, values = values, read = read };
            return JsonUtility.ToJson(dto);
        }

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
            SaveDto dto;
            try
            {
                dto = JsonUtility.FromJson<SaveDto>(json);
            }
            catch (Exception e)
            {
                // JsonUtility は不正 JSON / 非オブジェクトルートで ArgumentException 等を投げる
                throw new NovelSaveFormatException($"invalid save JSON: {e.Message}");
            }

            var values = new Dictionary<string, int>();
            if (dto.values != null)
                foreach (var entry in dto.values)
                    if (!string.IsNullOrEmpty(entry.key))
                        values[entry.key] = entry.value;

            var read = dto.read ?? new List<string>();
            return new NovelStateSnapshot(values, read);
        }
    }

    // セーブ JSON が壊れている/形式不正のときに投げる。TryDeserialize はこれを握って Empty を返す。
    public sealed class NovelSaveFormatException : Exception
    {
        public NovelSaveFormatException(string message) : base(message) { }
    }
}
