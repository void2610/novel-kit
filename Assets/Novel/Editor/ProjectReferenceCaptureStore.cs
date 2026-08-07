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

        // ディスクからの読込結果のドメイン内キャッシュ (OnGUI 等の高頻度呼び出しで毎回 I/O しないため)。
        // 新しいキャプチャは Latest が優先されるので、ドメイン中にディスク側を読み直す必要はない
        private static NovelProjectCapture.Snapshot? _fromDisk;
        private static bool _diskLoaded;

        /// <summary>ドメイン内の最新キャプチャ、無ければ永続化済みファイルを返す (どちらも無ければ null)。</summary>
        public static NovelProjectCapture.Snapshot? LoadOrLatest()
        {
            if (NovelProjectCapture.Latest != null) return NovelProjectCapture.Latest;
            if (_diskLoaded) return _fromDisk;
            _diskLoaded = true;
            _fromDisk = LoadFromDisk();
            return _fromDisk;
        }

        private static NovelProjectCapture.Snapshot? LoadFromDisk()
        {
            if (!File.Exists(FilePath)) return null;
            try
            {
                return ToSnapshot(JsonUtility.FromJson<Dto>(File.ReadAllText(FilePath)));
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[Novel] プロジェクトリファレンスのキャッシュ読み込みに失敗: {e}");
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
                Debug.LogWarning($"[Novel] プロジェクトリファレンスのキャッシュ保存に失敗: {e}");
            }
        }

        // JsonUtility 互換のプレーン DTO (readonly struct / DateTime は直接シリアライズできない)

        [Serializable]
        private class Dto
        {
            public AudioDto[] audio = Array.Empty<AudioDto>();
            public LayoutDto[] layouts = Array.Empty<LayoutDto>();
            public CharacterDto[] characters = Array.Empty<CharacterDto>();
            public string audioChannelType = "";
            public string portraitChannelType = "";
            public string characterCatalogType = "";
            public string capturedAt = "";
        }

        [Serializable]
        private class CharacterDto
        {
            public string id = "";
            public string displayName = "";
            public string defaultPortraitKey = "";
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
                characters = new CharacterDto[s.Characters.Count],
                audioChannelType = s.AudioChannelType,
                portraitChannelType = s.PortraitChannelType,
                characterCatalogType = s.CharacterCatalogType,
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
            for (var i = 0; i < s.Characters.Count; i++)
            {
                var c = s.Characters[i];
                dto.characters[i] = new CharacterDto { id = c.Id, displayName = c.DisplayName, defaultPortraitKey = c.DefaultPortraitKey ?? "" };
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

            var characters = new List<CharacterKeyInfo>(dto.characters.Length);
            foreach (var c in dto.characters)
                characters.Add(new CharacterKeyInfo(c.id, c.displayName, string.IsNullOrEmpty(c.defaultPortraitKey) ? null : c.defaultPortraitKey));

            DateTime.TryParse(dto.capturedAt, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var capturedAt);
            return new NovelProjectCapture.Snapshot(audio, layouts, characters, dto.audioChannelType, dto.portraitChannelType, dto.characterCatalogType, capturedAt);
        }
    }
}
