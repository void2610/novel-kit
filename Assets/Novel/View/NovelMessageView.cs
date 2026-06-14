#nullable enable
using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using Novel.Runtime;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Novel.View
{
    // INovelView の参考実装。進行ロジック（タイプライタ/速度/区間/待機/auto/skip）は Runtime の TextRevealEngine に委譲し、
    // 本クラスは TMP への文字列構築・可視文字数の反映・shake/wave 頂点演出という TMP 固有の描画 I/O だけを担う。
    // → 自前 View はこの薄いアダプタ部分だけ書けばよい（進行ロジックの再実装が不要）。
    // 送り入力は Advance() 経由（Input System / 旧 Input いずれにも結合しない）。
    public sealed class NovelMessageView : MonoBehaviour, INovelView
    {
        [SerializeField] private GameObject window = null!;
        [SerializeField] private TMP_Text nameLabel = null!;
        [SerializeField] private TMP_Text messageLabel = null!;

        [Header("Effects")]
        [SerializeField] private float shakeAmplitude = 3f;
        [SerializeField] private float waveAmplitude = 4f;
        [SerializeField] private float waveSpeed = 6f;

        [Header("Choices")]
        [SerializeField] private RectTransform choiceContainer = null!;
        [SerializeField] private Button choiceButtonPrefab = null!;

        private readonly IFrameClock _clock = new UnityFrameClock();
        private INovelPlaybackSettings _settings = new DefaultNovelPlaybackSettings();
        private TextRevealEngine? _engine;

        // game が再生設定（速度/auto delay/skip 方針）を供給する。未呼び出しなら Default
        public void Configure(INovelPlaybackSettings settings)
        {
            _settings = settings;
            // 設定差し替え時はエンジンを作り直す。auto/skip セッション状態は引き継ぐ
            if (_engine != null)
            {
                var (auto, skip) = (_engine.Auto, _engine.Skip);
                _engine = new TextRevealEngine(_settings, _clock) { Auto = auto, Skip = skip };
            }
        }

        private TextRevealEngine Engine => _engine ??= new TextRevealEngine(_settings, _clock);

        public bool IsAuto => Engine.Auto;
        public bool IsSkip => Engine.Skip;

        // game が送り入力（クリック/決定キー）を配線して呼ぶ
        public void Advance() => Engine.RequestAdvance();
        public void ToggleAuto() => Engine.Auto = !Engine.Auto;     // auto/skip は排他（エンジン側で保証）
        public void SetSkip(bool on) => Engine.Skip = on;
        public void ToggleSkip() => Engine.Skip = !Engine.Skip;

        public async UniTask ShowMessageAsync(NovelLine line, CancellationToken ct)
        {
            if (window != null) window.SetActive(true);
            nameLabel.text = line.DisplayName ?? "";

            var tokens = NovelTagLexer.Parse(line.Text);
            Engine.Build(tokens);   // 制御列・shake/wave 区間・総可視文字数を構築

            // TMP 表示文字列を構築（素テキストは noparse で包み、リテラル '<' 等が TMP タグと誤認されないようにする）
            var sb = new System.Text.StringBuilder();
            foreach (var t in tokens)
            {
                if (t.Kind == NovelTokenKind.Text)
                    sb.Append("<noparse>").Append(t.Payload).Append("</noparse>");
                else if (t.Kind == NovelTokenKind.TmpTag)
                    sb.Append(t.Payload);
            }

            messageLabel.text = sb.ToString();
            messageLabel.ForceMeshUpdate();
            int tmpTotal = messageLabel.textInfo.characterCount;
            messageLabel.maxVisibleCharacters = 0;

            using var animCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            var anim = AnimateEffectsAsync(animCts.Token).SuppressCancellationThrow();
            try
            {
                await Engine.RevealAsync(line.IsAlreadyRead,
                    v => messageLabel.maxVisibleCharacters = Mathf.Min(v, tmpTotal), ct);
            }
            finally
            {
                animCts.Cancel();
            }
        }

        public async UniTask<int> ShowChoicesAsync(IReadOnlyList<string> options, CancellationToken ct)
        {
            var tcs = new UniTaskCompletionSource<int>();
            var spawned = new List<GameObject>(options.Count);

            for (int i = 0; i < options.Count; i++)
            {
                int index = i;
                var button = Instantiate(choiceButtonPrefab, choiceContainer);
                var label = button.GetComponentInChildren<TMP_Text>();
                if (label != null) label.text = options[i];
                button.onClick.AddListener(() => tcs.TrySetResult(index));
                spawned.Add(button.gameObject);
            }

            try
            {
                using (ct.Register(() => tcs.TrySetCanceled()))
                    return await tcs.Task;
            }
            finally
            {
                foreach (var go in spawned) Destroy(go);
            }
        }

        // shake/wave の頂点アニメ。区間（可視文字 index）は Runtime のエンジンが算出済み。表示中の毎フレーム頂点をずらす
        private async UniTask AnimateEffectsAsync(CancellationToken ct)
        {
            var shake = Engine.ShakeSpans;
            var wave = Engine.WaveSpans;
            if (shake.Count == 0 && wave.Count == 0) return;

            while (!ct.IsCancellationRequested)
            {
                messageLabel.ForceMeshUpdate();
                var info = messageLabel.textInfo;
                int visible = messageLabel.maxVisibleCharacters;

                ApplyOffset(info, shake, visible, isWave: false);
                ApplyOffset(info, wave, visible, isWave: true);

                for (int m = 0; m < info.meshInfo.Length; m++)
                    messageLabel.UpdateGeometry(info.meshInfo[m].mesh, m);

                await UniTask.Yield(PlayerLoopTiming.LastPostLateUpdate, ct);
            }
        }

        private void ApplyOffset(TMP_TextInfo info, IReadOnlyList<(int start, int end)> ranges, int visible, bool isWave)
        {
            foreach (var (start, end) in ranges)
            {
                for (int i = start; i < end && i < visible && i < info.characterCount; i++)
                {
                    var ch = info.characterInfo[i];
                    if (!ch.isVisible) continue;

                    Vector3 offset = isWave
                        ? new Vector3(0f, Mathf.Sin(Time.time * waveSpeed + i * 0.5f) * waveAmplitude, 0f)
                        : new Vector3(UnityEngine.Random.Range(-shakeAmplitude, shakeAmplitude),
                                      UnityEngine.Random.Range(-shakeAmplitude, shakeAmplitude), 0f);

                    var verts = info.meshInfo[ch.materialReferenceIndex].vertices;
                    int vi = ch.vertexIndex;
                    for (int k = 0; k < 4; k++) verts[vi + k] += offset;
                }
            }
        }
    }
}
