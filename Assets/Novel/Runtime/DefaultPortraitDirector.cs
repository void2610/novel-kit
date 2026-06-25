#nullable enable
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace Novel.Runtime
{
    // <see cref="IPortraitDirector"/> の既定実装。 cast マップ (キャラ id → slot index) と現在の layout を保持し、
    // PortraitCommand の解決と StageCommand 適用時の差分処理 (退場 / 残留 / slot 変更 / 入場) を <see cref="IPortraitView"/> に委ねる。
    // 入退場アニメは View 実装側の責務 (Hide/Show の中でフェード等を行う想定)。
    public sealed class DefaultPortraitDirector : IPortraitDirector
    {
        private readonly IPortraitView _view;
        private readonly Dictionary<string, int> _cast = new();
        // Show のたびに最後に表示した portraitKey を記録し、 Stage 切替で slot 変更があった残留キャラの再表示に使う
        private readonly Dictionary<string, string> _lastPortraitKey = new();
        private PortraitLayout _layout;
        private bool _layoutInitialized;

        public DefaultPortraitDirector(IPortraitView view)
        {
            _view = view;
        }

        // 現在の場面 (テスト / デバッグ観測用)
        public PortraitLayout CurrentLayout => _layout;
        public IReadOnlyDictionary<string, int> CurrentCast => _cast;

        public UniTask StageAsync(PortraitLayout layout, IReadOnlyList<string> cast, CancellationToken ct)
        {
            var dict = new Dictionary<string, int>(cast.Count);
            for (var i = 0; i < cast.Count; i++) dict[cast[i]] = i;
            return StageAsync(layout, dict, ct);
        }

        public async UniTask StageAsync(PortraitLayout layout, IReadOnlyDictionary<string, int> cast, CancellationToken ct)
        {
            // 旧 cast と新 cast を比較して 3 種類に振り分ける:
            //   - 退場: 旧 cast にのみあるキャラ → 旧 slot を Hide
            //   - slot 変更: 両方にあるが index が違う → 旧 slot を Hide + SwitchLayout 後に新 slot で再 Show
            //   - 位置維持: 両方にあって index 同じ → 何もしない (View が SwitchLayout 内で滑らかに扱う)
            var toReshow = new List<(string character, int newSlot)>();
            foreach (var oldEntry in _cast)
            {
                if (!cast.TryGetValue(oldEntry.Key, out var newSlot))
                {
                    // 退場
                    await _view.HideAsync(oldEntry.Value, ct);
                }
                else if (newSlot != oldEntry.Value)
                {
                    // slot 変更: 旧 slot を Hide してから後で新 slot で再 Show
                    await _view.HideAsync(oldEntry.Value, ct);
                    if (_lastPortraitKey.TryGetValue(oldEntry.Key, out _))
                        toReshow.Add((oldEntry.Key, newSlot));
                }
            }

            _cast.Clear();
            foreach (var entry in cast) _cast[entry.Key] = entry.Value;
            _layout = layout;
            _layoutInitialized = true;
            await _view.SwitchLayoutAsync(layout, ct);

            // SwitchLayout 後に新 slot で再 Show (slot 変更があった残留キャラのみ)
            foreach (var (character, slot) in toReshow)
            {
                if (_lastPortraitKey.TryGetValue(character, out var key))
                    await _view.ShowAsync(slot, character, key, ct);
            }
        }

        public async UniTask ShowAsync(string character, string portraitKey, CancellationToken ct)
        {
            if (!_layoutInitialized)
            {
                // stage 未宣言で portrait が呼ばれた: single レイアウトを暗黙適用して View にも切替を通知する
                // (内部 _layout の更新だけだと View の slot が無いまま ShowAsync が走る不整合になるため、 SwitchLayoutAsync も await する)
                Debug.LogWarning($"[Novel] portrait '{character}' が stage 宣言なしで呼ばれました。 " +
                                 "暗黙に single レイアウトで slot 0 にフォールバックします。");
                _layout = PortraitLayout.Single;
                _layoutInitialized = true;
                await _view.SwitchLayoutAsync(_layout, ct);
            }
            var slotIndex = ResolveSlot(character);
            _lastPortraitKey[character] = portraitKey;
            await _view.ShowAsync(slotIndex, character, portraitKey, ct);
        }

        public async UniTask ExitAsync(string character, CancellationToken ct)
        {
            if (!_cast.TryGetValue(character, out var slotIndex)) return;
            _cast.Remove(character);
            _lastPortraitKey.Remove(character);
            await _view.HideAsync(slotIndex, ct);
        }

        public async UniTask ClearStageAsync(CancellationToken ct)
        {
            foreach (var entry in _cast) await _view.HideAsync(entry.Value, ct);
            _cast.Clear();
            _lastPortraitKey.Clear();
            // layout はリセットせず、 次の Stage 呼び出しまで現状維持 (画面が突然真っさらにならないように)
        }

        // cast に含まれない呼び出しは slot 0 にフォールバックして警告ログ (typo / 宣言漏れ検出のため)
        private int ResolveSlot(string character)
        {
            if (_cast.TryGetValue(character, out var slotIndex)) return slotIndex;
            Debug.LogWarning($"[Novel] portrait '{character}' は現在の stage cast に含まれていません。 " +
                             "slot 0 にフォールバックします (stage 宣言を確認してください)。");
            return 0;
        }
    }
}
