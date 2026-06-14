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
    // INovelView の参考実装。TMP タイプライタ + 選択肢 UI + shake/wave 頂点アニメ + auto/skip。
    // 速度/auto delay/skip 方針は INovelPlaybackSettings（game が Configure で供給、未供給なら Default）。
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

        private INovelPlaybackSettings _settings = new DefaultNovelPlaybackSettings();
        private bool _advanceRequested;
        private bool _auto;
        private bool _skip;
        private readonly List<(int start, int end)> _shake = new();
        private readonly List<(int start, int end)> _wave = new();

        // game が再生設定（速度/auto delay/skip 方針）を供給する。未呼び出しなら Default
        public void Configure(INovelPlaybackSettings settings) => _settings = settings;

        public bool IsAuto => _auto;
        public bool IsSkip => _skip;

        // game が送り入力（クリック/決定キー）を配線して呼ぶ
        public void Advance() => _advanceRequested = true;
        public void ToggleAuto() { _auto = !_auto; if (_auto) _skip = false; }   // auto/skip は排他
        public void SetSkip(bool on) { _skip = on; if (_skip) _auto = false; }
        public void ToggleSkip() => SetSkip(!_skip);

        public async UniTask ShowMessageAsync(NovelLine line, CancellationToken ct)
        {
            if (window != null) window.SetActive(true);
            nameLabel.text = line.DisplayName ?? "";

            var controls = new List<(int visible, NovelToken tok)>();
            var sb = new System.Text.StringBuilder();
            int visibleTotal = 0;
            int shakeStart = -1, waveStart = -1;
            _shake.Clear();
            _wave.Clear();

            foreach (var t in NovelTagLexer.Parse(line.Text))
            {
                switch (t.Kind)
                {
                    case NovelTokenKind.Text:
                        // 素テキストは noparse で包み、リテラル '<' 等が TMP タグと誤認されないようにする
                        sb.Append("<noparse>").Append(t.Payload).Append("</noparse>");
                        visibleTotal += t.Payload.Length;
                        break;
                    case NovelTokenKind.TmpTag:
                        sb.Append(t.Payload);
                        break;
                    case NovelTokenKind.ShakePush: shakeStart = visibleTotal; break;
                    case NovelTokenKind.ShakePop:
                        if (shakeStart >= 0) { _shake.Add((shakeStart, visibleTotal)); shakeStart = -1; }
                        break;
                    case NovelTokenKind.WavePush: waveStart = visibleTotal; break;
                    case NovelTokenKind.WavePop:
                        if (waveStart >= 0) { _wave.Add((waveStart, visibleTotal)); waveStart = -1; }
                        break;
                    default:
                        controls.Add((visibleTotal, t));
                        break;
                }
            }

            messageLabel.text = sb.ToString();
            messageLabel.ForceMeshUpdate();
            int total = messageLabel.textInfo.characterCount;
            messageLabel.maxVisibleCharacters = 0;

            using var animCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            var anim = AnimateEffectsAsync(animCts.Token).SuppressCancellationThrow();
            try
            {
                // skip 中はタイプライタを飛ばして即時全表示（SkipUnread=false なら既読行のみ対象）
                bool skipThisLine = _skip && (_settings.SkipUnread || line.IsAlreadyRead);
                if (!skipThisLine)
                {
                    int shown = 0;
                    int ci = 0;
                    bool fast = false;
                    float speed = _settings.CharsPerSecond;
                    float acc = 0f;

                    while (shown < total)
                    {
                        while (ci < controls.Count && controls[ci].visible <= shown)
                        {
                            var tok = controls[ci].tok;
                            ci++;
                            switch (tok.Kind)
                            {
                                case NovelTokenKind.Wait:
                                    // skip/auto 中は明示待機も飛ばす
                                    if (!fast && !_skip) await UniTask.Delay(TimeSpan.FromSeconds(tok.Value), cancellationToken: ct);
                                    break;
                                case NovelTokenKind.ClickWait:
                                    if (!_skip && !_auto) await WaitAdvanceAsync(ct);
                                    break;
                                case NovelTokenKind.Fast:
                                    fast = true;
                                    break;
                                case NovelTokenKind.SpeedPush:
                                    speed = _settings.CharsPerSecond * (tok.Value <= 0f ? 1f : tok.Value);
                                    break;
                                case NovelTokenKind.SpeedPop:
                                    speed = _settings.CharsPerSecond;
                                    break;
                            }
                        }

                        if (fast || _skip) break;

                        acc += speed * Time.deltaTime;
                        while (acc >= 1f && shown < total) { shown++; acc -= 1f; }
                        messageLabel.maxVisibleCharacters = shown;

                        if (_advanceRequested) { _advanceRequested = false; break; }
                        await UniTask.Yield(PlayerLoopTiming.Update, ct);
                    }
                }

                messageLabel.maxVisibleCharacters = total;
                await WaitNextLineAsync(line.IsAlreadyRead, ct);
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

        // 行末の進行待ち。skip は既読/SkipUnread で即進行、auto は AutoAdvanceDelay 待ち（送り入力で短絡）
        private async UniTask WaitNextLineAsync(bool alreadyRead, CancellationToken ct)
        {
            _advanceRequested = false;
            if (_skip && (_settings.SkipUnread || alreadyRead)) return;
            if (_auto)
            {
                float t = 0f;
                while (t < _settings.AutoAdvanceDelay && !_advanceRequested)
                {
                    t += Time.deltaTime;
                    await UniTask.Yield(PlayerLoopTiming.Update, ct);
                }
                _advanceRequested = false;
                return;
            }
            await UniTask.WaitUntil(() => _advanceRequested, cancellationToken: ct);
            _advanceRequested = false;
        }

        // shake/wave の頂点アニメ。表示中の毎フレーム、対象文字範囲のメッシュ頂点をずらす
        private async UniTask AnimateEffectsAsync(CancellationToken ct)
        {
            if (_shake.Count == 0 && _wave.Count == 0) return;

            while (!ct.IsCancellationRequested)
            {
                messageLabel.ForceMeshUpdate();
                var info = messageLabel.textInfo;
                int visible = messageLabel.maxVisibleCharacters;

                ApplyOffset(info, _shake, visible, isWave: false);
                ApplyOffset(info, _wave, visible, isWave: true);

                for (int m = 0; m < info.meshInfo.Length; m++)
                    messageLabel.UpdateGeometry(info.meshInfo[m].mesh, m);

                await UniTask.Yield(PlayerLoopTiming.LastPostLateUpdate, ct);
            }
        }

        private void ApplyOffset(TMP_TextInfo info, List<(int start, int end)> ranges, int visible, bool isWave)
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

        // <p>（クリック待ち）専用。auto/skip は呼び出し側で短絡済み
        private async UniTask WaitAdvanceAsync(CancellationToken ct)
        {
            _advanceRequested = false;
            await UniTask.WaitUntil(() => _advanceRequested, cancellationToken: ct);
            _advanceRequested = false;
        }
    }
}
