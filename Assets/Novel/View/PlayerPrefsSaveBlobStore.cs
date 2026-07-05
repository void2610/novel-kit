#nullable enable
using System.Threading;
using Cysharp.Threading.Tasks;
using Novel.Runtime;
using UnityEngine;

namespace Novel.View
{
    // INovelSaveBlobStore の Unity 実装(参考)。JsonSaveStore(novel-kit 内部完結モード)に組み合わせて使う。
    // セーブ JSON を PlayerPrefs の 1 キーへ保存する小規模タイトル向けの箱出し実装。
    // 自前 JSON セーブ機構を持つなら、これではなく自前 ISaveStore + NovelSaveSerializer / NovelSaveData を使う。
    public sealed class PlayerPrefsSaveBlobStore : INovelSaveBlobStore
    {
        public const string DefaultKey = "novel.save";

        private readonly string _key;

        public PlayerPrefsSaveBlobStore(string key = DefaultKey) => _key = key;

        public UniTask WriteAsync(string json, CancellationToken ct)
        {
            PlayerPrefs.SetString(_key, json);
            PlayerPrefs.Save();
            return UniTask.CompletedTask;
        }

        public UniTask<string?> ReadAsync(CancellationToken ct)
            => UniTask.FromResult(PlayerPrefs.HasKey(_key) ? PlayerPrefs.GetString(_key) : null);
    }
}
