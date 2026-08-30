#nullable enable
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using Novel.Assets;
using Novel.Runtime;
using UnityEditor;
using UnityEngine;

namespace Novel.Editor
{
    /// <summary>
    /// <see cref="NovelProjectCapture"/> の購読側 (project-reference ADR)。再生時の DI ビルドで届いた
    /// スナップショットを種別ごとにマージして Library/ 配下へ永続化し、ドメインリロード / エディタ再起動を
    /// 跨いでプロジェクトリファレンスに供給する。
    /// - 空の種別 (EnumerateKeys 等が空を返す配線) は「列挙未提供」とみなし、以前の実データを保持する。
    ///   タイトル画面など novel 未配線のスコープのビルドが、キャプチャ済みの目録を消さないため
    ///   (ScenarioKeyValidator の「空 = 列挙未提供」と同じ割り切り)。
    /// - Edit Mode でのコンテナビルド (EditMode テスト・エディタツール) は実プロジェクトの配線ではないため採用しない。
    /// - AudioKeyInfo.Asset は GUID で永続化し、読み出しは常に GUID からアセット実体を引く
    ///   (再生終了でランタイム側の参照が破棄されても試聴が生き続ける)。
    /// </summary>
    [InitializeOnLoad]
    public static class ProjectReferenceCaptureStore
    {
        private const string CacheDirectory = "Library/NovelKit";
        private const string FilePath = CacheDirectory + "/ProjectReferenceCapture.json";

        static ProjectReferenceCaptureStore()
        {
            NovelProjectCapture.Captured += OnCaptured;
        }

        // マージ済みキャプチャのドメイン内キャッシュ。_dto (GUID 形式・ディスクと同内容) が正で、
        // _snapshot はその実体化。OnGUI 等の高頻度呼び出しで毎回 I/O しないためのもの
        private static Dto? _dto;
        private static NovelProjectCapture.Snapshot? _snapshot;
        private static bool _loaded;

        /// <summary>マージ済みの最新キャプチャを返す (一度もキャプチャされていなければ null)。</summary>
        public static NovelProjectCapture.Snapshot? LoadOrLatest()
        {
            EnsureLoaded();
            // アセット削除や Unload で試聴用参照が死んでいたら GUID から引き直す
            if (_snapshot != null && HasDestroyedAsset(_snapshot)) _snapshot = ToSnapshot(_dto!);
            return _snapshot;
        }

        private static void EnsureLoaded()
        {
            if (_loaded) return;
            _loaded = true;
            _dto = LoadFromDisk();
            _snapshot = _dto == null ? null : ToSnapshot(_dto);
        }

        private static void OnCaptured(NovelProjectCapture.Snapshot snapshot)
        {
            if (!(PlayModeGateForTest ?? EditorApplication.isPlayingOrWillChangePlaymode)) return;
            EnsureLoaded();
            // ToDto はアセット参照が生きているこの時点で GUID を確定させる
            _dto = Merge(_dto, ToDto(snapshot));
            _snapshot = ToSnapshot(_dto);
            Save(_dto);
        }

        // ---- テスト用シーム (実プロジェクトの Library キャッシュを汚さずにキャプチャ経路を検証する) ----

        internal static bool? PlayModeGateForTest;    // null = 実際の Play Mode 状態を見る
        internal static string? FilePathForTest;      // null = 既定の Library パス

        internal static void ResetForTest()
        {
            _loaded = false;
            _dto = null;
            _snapshot = null;
        }

        /// <summary>
        /// 種別ごとに「空 = 列挙未提供」とみなし、以前の実データ (と対応する取得元型名) を保持する。
        /// 非空の種別は新しいキャプチャで置き換える。
        /// </summary>
        private static Dto Merge(Dto? old, Dto incoming)
        {
            if (old == null) return incoming;
            if (incoming.audio.Length == 0 && old.audio.Length > 0)
            {
                incoming.audio = old.audio;
                incoming.audioChannelType = old.audioChannelType;
            }
            // コマンドは「モジュール未登録の配線 = 未提供」とみなす (novel 未配線スコープが目録を消さないため)
            if (incoming.commands.Length == 0 && old.commands.Length > 0)
                incoming.commands = old.commands;
            if (incoming.characters.Length == 0 && old.characters.Length > 0)
            {
                incoming.characters = old.characters;
                incoming.characterCatalogType = old.characterCatalogType;
            }
            // スプライトローダは型名を presence マーカーにする (プレフィックス空文字は正当な値のため
            // 空判定には使えない)。未キャプチャの配線が、以前に判明した root を消さないようにする
            if (incoming.spriteLoaderType.Length == 0 && old.spriteLoaderType.Length > 0)
            {
                incoming.spriteLoaderType = old.spriteLoaderType;
                incoming.spriteKeyPrefix = old.spriteKeyPrefix;
                incoming.hasSpriteKeyPrefix = old.hasSpriteKeyPrefix;
            }
            // 構図は既定実装が標準 5 構図を返すため「標準構図のまま = 未提供」とみなす。
            // 独自構図から意図的に標準へ戻したい場合は Library/NovelKit を削除して再生し直す
            if (old.layouts.Length > 0 && (incoming.layouts.Length == 0 || IsDefaultLayouts(incoming.layouts)))
            {
                incoming.layouts = old.layouts;
                incoming.portraitChannelType = old.portraitChannelType;
            }
            return incoming;
        }

        private static bool IsDefaultLayouts(LayoutDto[] layouts)
        {
            var defaults = StageLayoutInfo.Defaults;
            if (layouts.Length != defaults.Count) return false;
            for (var i = 0; i < layouts.Length; i++)
            {
                if (layouts[i].id != defaults[i].Id || layouts[i].slotCount != defaults[i].SlotCount ||
                    layouts[i].note != (defaults[i].Note ?? ""))
                    return false;
            }
            return true;
        }

        // テストから種別マージ (+ DTO 往復のシリアライズ) を検証するための入口
        internal static NovelProjectCapture.Snapshot MergeForTest(
            NovelProjectCapture.Snapshot? old, NovelProjectCapture.Snapshot incoming)
            => ToSnapshot(Merge(old == null ? null : ToDto(old), ToDto(incoming)));

        private static bool HasDestroyedAsset(NovelProjectCapture.Snapshot snapshot)
        {
            foreach (var key in snapshot.AudioKeys)
                if (key.Asset is UnityEngine.Object obj && obj == null)
                    return true;
            return false;
        }

        private static string TargetPath => FilePathForTest ?? FilePath;

        private static Dto? LoadFromDisk()
        {
            if (!File.Exists(TargetPath)) return null;
            try
            {
                return JsonUtility.FromJson<Dto>(File.ReadAllText(TargetPath));
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[Novel] プロジェクトリファレンスのキャッシュ読み込みに失敗: {e}");
                return null;
            }
        }

        private static void Save(Dto dto)
        {
            try
            {
                var path = TargetPath;
                System.IO.Directory.CreateDirectory(Path.GetDirectoryName(path)!);
                File.WriteAllText(path, JsonUtility.ToJson(dto));
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
            public CommandDto[] commands = Array.Empty<CommandDto>();
            public string audioChannelType = "";
            public string portraitChannelType = "";
            public string characterCatalogType = "";
            public string spriteLoaderType = "";
            public string spriteKeyPrefix = "";
            public bool hasSpriteKeyPrefix;   // ローダが ISpriteKeyPrefix を名乗ったか (空プレフィックスの確定と不明の区別)
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
        private class CommandDto
        {
            public string name = "";
            public string commandType = "";
            public string moduleType = "";
            public string description = "";
            public string[] paramNames = Array.Empty<string>();
            public string[] paramTypes = Array.Empty<string>();
            public string[] paramDescriptions = Array.Empty<string>();
        }

        [Serializable]
        private class AudioDto
        {
            public string key = "";
            public int kind;
            public string note = "";
            public string guid = "";   // AudioKeyInfo.Asset の GUID (試聴用・無ければ空)
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
                commands = new CommandDto[s.Commands.Count],
                audioChannelType = s.AudioChannelType,
                portraitChannelType = s.PortraitChannelType,
                characterCatalogType = s.CharacterCatalogType,
                spriteLoaderType = s.SpriteLoaderType,
                spriteKeyPrefix = s.SpriteKeyPrefix ?? "",
                hasSpriteKeyPrefix = s.SpriteKeyPrefix != null,
                capturedAt = s.CapturedAt.ToString("o", CultureInfo.InvariantCulture),
            };
            for (var i = 0; i < s.AudioKeys.Count; i++)
            {
                var k = s.AudioKeys[i];
                dto.audio[i] = new AudioDto { key = k.Key, kind = (int)k.Kind, note = k.Note ?? "", guid = GuidOf(k.Asset) };
            }
            for (var i = 0; i < s.Layouts.Count; i++)
            {
                var l = s.Layouts[i];
                dto.layouts[i] = new LayoutDto { id = l.Id, slotCount = l.SlotCount, note = l.Note ?? "" };
            }
            for (var i = 0; i < s.Commands.Count; i++)
            {
                var c = s.Commands[i];
                dto.commands[i] = new CommandDto
                {
                    name = c.Name, commandType = c.CommandType, moduleType = c.ModuleType, description = c.Description ?? "",
                    paramNames = c.Parameters.Select(p => p.Name).ToArray(),
                    paramTypes = c.Parameters.Select(p => p.TypeName).ToArray(),
                    paramDescriptions = c.Parameters.Select(p => p.Description ?? "").ToArray(),
                };
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
                audio.Add(new AudioKeyInfo(a.key, (AudioKeyKind)a.kind, string.IsNullOrEmpty(a.note) ? null : a.note, LoadByGuid(a.guid)));

            var layouts = new List<StageLayoutInfo>(dto.layouts.Length);
            foreach (var l in dto.layouts)
                layouts.Add(new StageLayoutInfo(l.id, l.slotCount, string.IsNullOrEmpty(l.note) ? null : l.note));

            var characters = new List<CharacterKeyInfo>(dto.characters.Length);
            foreach (var c in dto.characters)
                characters.Add(new CharacterKeyInfo(c.id, c.displayName, string.IsNullOrEmpty(c.defaultPortraitKey) ? null : c.defaultPortraitKey));

            var commands = new List<CommandKeyInfo>(dto.commands.Length);
            foreach (var c in dto.commands)
            {
                var parameters = new List<CommandParameterInfo>(c.paramNames.Length);
                for (var i = 0; i < c.paramNames.Length; i++)
                    parameters.Add(new CommandParameterInfo(c.paramNames[i], i < c.paramTypes.Length ? c.paramTypes[i] : "",
                        i < c.paramDescriptions.Length ? c.paramDescriptions[i] : null));
                commands.Add(new CommandKeyInfo(c.name, c.commandType, c.moduleType, parameters, c.description));
            }

            DateTime.TryParse(dto.capturedAt, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var capturedAt);
            return new NovelProjectCapture.Snapshot(
                audio, layouts, characters,
                dto.audioChannelType, dto.portraitChannelType, dto.characterCatalogType, capturedAt,
                dto.spriteLoaderType, dto.hasSpriteKeyPrefix ? dto.spriteKeyPrefix : null, commands);
        }

        // AudioKeyInfo.Asset (試聴用のアセット参照) は JSON に GUID として永続化し、読み込み時に実体へ戻す

        private static string GuidOf(object? asset)
        {
            if (asset is not UnityEngine.Object obj || obj == null) return "";
            var path = AssetDatabase.GetAssetPath(obj);
            return string.IsNullOrEmpty(path) ? "" : AssetDatabase.AssetPathToGUID(path);
        }

        private static UnityEngine.Object? LoadByGuid(string guid)
        {
            if (string.IsNullOrEmpty(guid)) return null;
            var path = AssetDatabase.GUIDToAssetPath(guid);
            return string.IsNullOrEmpty(path) ? null : AssetDatabase.LoadMainAssetAtPath(path);
        }
    }
}
