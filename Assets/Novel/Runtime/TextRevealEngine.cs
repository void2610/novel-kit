#nullable enable
using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;

namespace Novel.Runtime
{
    // 行内インラインタグの逐次 Reveal を駆動する UI 非依存の進行エンジン（inline-tags ADR の帰結を Runtime に統合）。
    // 速度蓄積・<w>/<p>/<fast>/<speed> 解釈・auto/skip・shake/wave 区間算出をここに集約し、View は
    // 「可視文字数の反映」と「頂点演出」だけに専念する（自前 View が進行ロジックを再実装しなくて済む）。
    // 時間は IFrameClock 経由で受けるため UnityEngine 非依存で、fake clock によりヘッドレス検証できる。
    public sealed class TextRevealEngine
    {
        private readonly INovelPlaybackSettings _settings;
        private readonly IFrameClock _clock;

        // 再生セッション状態（特定 View 窓ではなくセッションの状態。auto/skip は排他）
        private bool _advance;
        private bool _auto;
        private bool _skip;

        public bool Auto { get => _auto; set { _auto = value; if (value) _skip = false; } }
        public bool Skip { get => _skip; set { _skip = value; if (value) _auto = false; } }
        public void RequestAdvance() => _advance = true;

        // Build で算出する行内データ（可視文字 index 単位。TMP/UI 非依存）
        private readonly List<(int visible, NovelToken tok)> _controls = new();
        private readonly List<(int start, int end)> _shake = new();
        private readonly List<(int start, int end)> _wave = new();
        private int _total;

        public IReadOnlyList<(int start, int end)> ShakeSpans => _shake;
        public IReadOnlyList<(int start, int end)> WaveSpans => _wave;
        public int TotalVisibleCount => _total;

        public TextRevealEngine(INovelPlaybackSettings settings, IFrameClock clock)
        {
            _settings = settings;
            _clock = clock;
        }

        // トークン列から制御列・shake/wave 区間・総可視文字数を構築する。区間はスタックで入れ子/重複に対応する。
        public int Build(IReadOnlyList<NovelToken> tokens)
        {
            _controls.Clear();
            _shake.Clear();
            _wave.Clear();
            int visible = 0;
            var shakeStack = new Stack<int>();
            var waveStack = new Stack<int>();

            foreach (var t in tokens)
            {
                switch (t.Kind)
                {
                    case NovelTokenKind.Text:
                        visible += t.Payload.Length;
                        break;
                    case NovelTokenKind.TmpTag:
                    case NovelTokenKind.Ignored:
                        break;   // 可視文字数に数えない
                    case NovelTokenKind.ShakePush:
                        shakeStack.Push(visible);
                        break;
                    case NovelTokenKind.ShakePop:
                        if (shakeStack.Count > 0) _shake.Add((shakeStack.Pop(), visible));
                        break;
                    case NovelTokenKind.WavePush:
                        waveStack.Push(visible);
                        break;
                    case NovelTokenKind.WavePop:
                        if (waveStack.Count > 0) _wave.Add((waveStack.Pop(), visible));
                        break;
                    default:
                        // Wait/ClickWait/Fast/SpeedPush/SpeedPop は可視位置つきの制御として保持
                        _controls.Add((visible, t));
                        break;
                }
            }

            _total = visible;
            return visible;
        }

        // 行を逐次 Reveal し、可視文字数が変わるたび onVisible を呼ぶ。Reveal 後は行末の送り待ちまで行う。
        // 事前に Build を呼んでおくこと。
        public async UniTask RevealAsync(bool alreadyRead, Action<int> onVisible, CancellationToken ct)
        {
            bool skipThisLine = _skip && (_settings.SkipUnread || alreadyRead);

            if (!skipThisLine)
            {
                int shown = 0;
                int ci = 0;
                bool fast = false;
                float speed = _settings.CharsPerSecond;
                var speedStack = new Stack<float>();
                float acc = 0f;
                _advance = false;

                while (shown < _total)
                {
                    while (ci < _controls.Count && _controls[ci].visible <= shown)
                    {
                        var tok = _controls[ci].tok;
                        ci++;
                        switch (tok.Kind)
                        {
                            case NovelTokenKind.Wait:
                                // 明示待機。クリック/skip で打ち切れる（<w> をまたいでも即時全表示が効く）
                                if (!fast && !_skip)
                                {
                                    float remain = tok.Value;
                                    while (remain > 0f && !_advance && !_skip)
                                    {
                                        await _clock.NextFrameAsync(ct);
                                        remain -= _clock.DeltaTime;
                                    }
                                }
                                break;
                            case NovelTokenKind.ClickWait:
                                if (!_skip && !_auto)
                                {
                                    _advance = false;
                                    while (!_advance && !_skip && !_auto)
                                        await _clock.NextFrameAsync(ct);
                                    _advance = false;
                                }
                                break;
                            case NovelTokenKind.Fast:
                                fast = true;
                                break;
                            case NovelTokenKind.SpeedPush:
                                speedStack.Push(speed);
                                speed = _settings.CharsPerSecond * (tok.Value <= 0f ? 1f : tok.Value);
                                break;
                            case NovelTokenKind.SpeedPop:
                                speed = speedStack.Count > 0 ? speedStack.Pop() : _settings.CharsPerSecond;
                                break;
                        }
                    }

                    if (fast || _skip) break;

                    acc += speed * _clock.DeltaTime;
                    while (acc >= 1f && shown < _total) { shown++; acc -= 1f; }
                    onVisible(shown);

                    if (_advance) { _advance = false; break; }   // クリックで残りを即時全表示
                    await _clock.NextFrameAsync(ct);
                }
            }

            onVisible(_total);
            await WaitForAdvanceAsync(alreadyRead, ct);
        }

        // 行末の送り待ち。待機中の auto/skip 切り替えにも毎フレーム反応する
        public async UniTask WaitForAdvanceAsync(bool alreadyRead, CancellationToken ct)
        {
            _advance = false;
            float elapsed = 0f;
            while (true)
            {
                if (_advance) break;
                if (_skip && (_settings.SkipUnread || alreadyRead)) break;
                if (_auto && elapsed >= _settings.AutoAdvanceDelay) break;
                elapsed += _clock.DeltaTime;
                await _clock.NextFrameAsync(ct);
            }
            _advance = false;
        }
    }
}
