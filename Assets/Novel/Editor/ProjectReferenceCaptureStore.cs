#nullable enable
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using Novel.Assets;
using Novel.Runtime;
using UnityEditor;
using UnityEngine;

namespace Novel.Editor
{
    /// <summary>
    /// <see cref="NovelProjectCapture"/> の購読側 (project-reference ADR)。DI ビルド時のスナップショットを
    /// Library/ 配下へ永続化し、ドメインリロード / エディタ再起動を跨いでプロジェクトリファレンスに供給する。
    /// </summary>
    [InitializeOnLoad]
    public static class ProjectReferenceCaptureStore
    {
        private const string CacheDirectory = "Library/NovelKit";
        private const string FilePath = CacheDirectory + "/ProjectReferenceCapture.json";

        static ProjectReferenceCaptureStore()
        {
            NovelProjectCapture.Captured += Save;
        }

        /// <summary>ドメイン内の最新キャプチャ、無ければ永続化済みファイルを返す (どちらも無ければ null)。</summary>
        public static NovelProjectCapture.Snapshot? LoadOrLatest()
        {
            if (NovelProjectCapture.Latest != null) return NovelProjectCapture.Latest;
            if (!File.Exists(FilePath)) return null;
            try
            {
                return ToSnapshot(JsonUtility.FromJson<Dto>(File.ReadAllText(FilePath)));
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[Novel] プロジェクトリファレンスのキャッシュ読み込みに失敗: {e.Message}");
                return null;
            }
        }

        private static void Save(NovelProjectCapture.Snapshot snapshot)
        {
            try
            {
                System.IO.Directory.CreateDirectory(CacheDirectory);
                File.WriteAllText(FilePath, JsonUtility.ToJson(ToDto(snapshot)));
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[Novel] プロジェクトリファレンスのキャッシュ保存に失敗: {e.Message}");
            }
        }

        // JsonUtility 互換のプレーン DTO (readonly struct / DateTime は直接シリアライズできない)

        [Serializable]
        private class Dto
        {
            public AudioDto[] audio = Array.Empty<AudioDto>();
            public LayoutDto[] layouts = Array.Empty<LayoutDto>();
            public string audioChannelType = "";
            public string portraitChannelType = "";
            public string capturedAt = "";
        }

        [Serializable]
        private class AudioDto
        {
            public string key = "";
            public int kind;
            public string note = "";
        }

        [Serializable]
        private class LayoutDto
        {
            public string id = "";
            public int slotCount;
            public string note = "";
        }

        private static Dto ToDto(NovelProjectCapture.Snapshot s)
        {
            var dto = new Dto
            {
                audio = new AudioDto[s.AudioKeys.Count],
                layouts = new LayoutDto[s.Layouts.Count],
                audioChannelType = s.AudioChannelType,
                portraitChannelType = s.PortraitChannelType,
                capturedAt = s.CapturedAt.ToString("o", CultureInfo.InvariantCulture),
            };
            for (var i = 0; i < s.AudioKeys.Count; i++)
            {
                var k = s.AudioKeys[i];
                dto.audio[i] = new AudioDto { key = k.Key, kind = (int)k.Kind, note = k.Note ?? "" };
            }
            for (var i = 0; i < s.Layouts.Count; i++)
            {
                var l = s.Layouts[i];
                dto.layouts[i] = new LayoutDto { id = l.Id, slotCount = l.SlotCount, note = l.Note ?? "" };
            }
            return dto;
        }

        private static NovelProjectCapture.Snapshot ToSnapshot(Dto dto)
        {
            var audio = new List<AudioKeyInfo>(dto.audio.Length);
            foreach (var a in dto.audio)
                audio.Add(new AudioKeyInfo(a.key, (AudioKeyKind)a.kind, string.IsNullOrEmpty(a.note) ? null : a.note));

            var layouts = new List<StageLayoutInfo>(dto.layouts.Length);
            foreach (var l in dto.layouts)
                layouts.Add(new StageLayoutInfo(l.id, l.slotCount, string.IsNullOrEmpty(l.note) ? null : l.note));

            DateTime.TryParse(dto.capturedAt, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var capturedAt);
            return new NovelProjectCapture.Snapshot(audio, layouts, dto.audioChannelType, dto.portraitChannelType, capturedAt);
        }
    }
}
