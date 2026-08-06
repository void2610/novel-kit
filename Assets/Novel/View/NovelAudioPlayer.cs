#nullable enable
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using Novel.Runtime;
using UnityEngine;

namespace Novel.View
{
    /// <summary>
    /// <see cref="IAudioChannel"/> の参考実装 (project-reference ADR)。<see cref="ScriptableAudioCatalog"/> で
    /// キーを解決し、AudioSource で鳴らす。機能は最小限 — BGM 再生/切替/停止・SE ワンショット/連打のみ。
    /// クロスフェード・ミキサー連携・音量設定連動が要る game は自前の IAudioChannel を後勝ち登録する。
    ///
    /// 使い方: シーンに置いてカタログを割り当て、LifetimeScope で
    /// <c>builder.RegisterComponent(player).As&lt;IAudioChannel&gt;()</c> と登録する。
    /// AudioSource 未割り当てなら初回再生時に自動生成する (BGM 用はループ設定)。
    /// </summary>
    public sealed class NovelAudioPlayer : MonoBehaviour, IAudioChannel
    {
        [SerializeField] private ScriptableAudioCatalog? catalog;
        [SerializeField] private AudioSource? bgmSource;
        [SerializeField] private AudioSource? seSource;

        public UniTask PlaySeAsync(string seKey, CancellationToken ct)
        {
            // 行の進行は SE の鳴り終わりを待たない
            if (TryResolve(seKey, AudioKeyKind.Se, out var clip)) Se.PlayOneShot(clip);
            return UniTask.CompletedTask;
        }

        public async UniTask PlaySeLoopAsync(string seKey, float interval, int count, CancellationToken ct)
        {
            if (!TryResolve(seKey, AudioKeyKind.Se, out var clip)) return;
            for (var i = 0; i < count; i++)
            {
                ct.ThrowIfCancellationRequested();
                Se.PlayOneShot(clip);
                // ハンドラが await するため、鳴らし切るまでが演出の「間」になる (最後の 1 回の後は待たない)
                if (i < count - 1)
                    await UniTask.Delay(System.TimeSpan.FromSeconds(interval), cancellationToken: ct);
            }
        }

        public void PlayBgm(string bgmKey)
        {
            // 空キーはハンドラ側で StopBgm に振り分けられるため、ここに来るのは実キーのみ
            if (!TryResolve(bgmKey, AudioKeyKind.Bgm, out var clip)) return;
            if (Bgm.isPlaying && Bgm.clip == clip) return;   // 同じ曲は流し直さない
            Bgm.clip = clip;
            Bgm.Play();
        }

        public void StopBgm()
        {
            if (bgmSource != null) bgmSource.Stop();
        }

        public IEnumerable<AudioKeyInfo> EnumerateKeys() =>
            catalog != null ? catalog.EnumerateKeys() : System.Array.Empty<AudioKeyInfo>();

        // Awake のタイミングに依存しないよう、AudioSource は初回利用時に用意する
        private AudioSource Bgm
        {
            get
            {
                if (bgmSource == null)
                {
                    bgmSource = gameObject.AddComponent<AudioSource>();
                    bgmSource.playOnAwake = false;
                    bgmSource.loop = true;
                }
                return bgmSource;
            }
        }

        private AudioSource Se
        {
            get
            {
                if (seSource == null)
                {
                    seSource = gameObject.AddComponent<AudioSource>();
                    seSource.playOnAwake = false;
                }
                return seSource;
            }
        }

        private bool TryResolve(string key, AudioKeyKind kind, out AudioClip clip)
        {
            clip = null!;
            if (string.IsNullOrEmpty(key)) return false;
            if (catalog == null)
            {
                Debug.LogWarning("[Novel] NovelAudioPlayer にカタログが割り当てられていません。", this);
                return false;
            }
            if (!catalog.TryGet(key, kind, out clip))
            {
                Debug.LogWarning($"[Novel] AudioCatalog に {kind} キー '{key}' がありません。", this);
                return false;
            }
            return true;
        }
    }
}
