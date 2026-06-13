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
    // INovelView の参考実装。TMP タイプライタ + 選択肢 UI。game は自前実装に差し替え可。
    // 送り入力は Advance() 経由（Input System / 旧 Input いずれにも結合しない）。
    // TODO: shake/wave の頂点アニメ・<noparse>外の '<' エスケープ・既読色変化
    public sealed class NovelMessageView : MonoBehaviour, INovelView
    {
        [SerializeField] private GameObject window = null!;
        [SerializeField] private TMP_Text nameLabel = null!;
        [SerializeField] private TMP_Text messageLabel = null!;
        [SerializeField] private float charsPerSecond = 30f;

        [Header("Choices")]
        [SerializeField] private RectTransform choiceContainer = null!;
        [SerializeField] private Button choiceButtonPrefab = null!;

        private bool _advanceRequested;

        // game が送り入力（クリック/決定キー）を配線して呼ぶ
        public void Advance() => _advanceRequested = true;

        public async UniTask ShowMessageAsync(NovelLine line, CancellationToken ct)
        {
            if (window != null) window.SetActive(true);
            nameLabel.text = line.DisplayName ?? "";

            // 字句解析 → 表示文字列 + 制御トークンの可視位置
            var controls = new List<(int visible, NovelToken tok)>();
            var sb = new System.Text.StringBuilder();
            int visibleTotal = 0;
            foreach (var t in NovelTagLexer.Parse(line.Text))
            {
                switch (t.Kind)
                {
                    case NovelTokenKind.Text:
                        sb.Append(t.Payload);
                        visibleTotal += t.Payload.Length;
                        break;
                    case NovelTokenKind.TmpTag:
                        sb.Append(t.Payload);
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

            int shown = 0;
            int ci = 0;
            bool fast = false;
            float speed = charsPerSecond;
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
                            if (!fast) await UniTask.Delay(TimeSpan.FromSeconds(tok.Value), cancellationToken: ct);
                            break;
                        case NovelTokenKind.ClickWait:
                            await WaitAdvanceAsync(ct);
                            break;
                        case NovelTokenKind.Fast:
                            fast = true;
                            break;
                        case NovelTokenKind.SpeedPush:
                            speed = charsPerSecond * (tok.Value <= 0f ? 1f : tok.Value);
                            break;
                        case NovelTokenKind.SpeedPop:
                            speed = charsPerSecond;
                            break;
                    }
                }

                if (fast) break;

                acc += speed * Time.deltaTime;
                while (acc >= 1f && shown < total)
                {
                    shown++;
                    acc -= 1f;
                }
                messageLabel.maxVisibleCharacters = shown;

                if (_advanceRequested)
                {
                    _advanceRequested = false;
                    break;   // 途中送り → 全文即時表示
                }
                await UniTask.Yield(PlayerLoopTiming.Update, ct);
            }

            messageLabel.maxVisibleCharacters = total;
            await WaitAdvanceAsync(ct);
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

        private async UniTask WaitAdvanceAsync(CancellationToken ct)
        {
            _advanceRequested = false;
            await UniTask.WaitUntil(() => _advanceRequested, cancellationToken: ct);
            _advanceRequested = false;
        }
    }
}
