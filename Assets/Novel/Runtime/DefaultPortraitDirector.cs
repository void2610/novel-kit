#nullable enable
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace Novel.Runtime
{
    // <see cref="IPortraitDirector"/> の既定実装。 cast マップ (キャラ id → slot index) と現在の layout を保持し、
    // PortraitCommand の解決と StageCommand 適用時の差分処理 (退場 / 残留 / 入場) を <see cref="IPortraitView"/> に委ねる。
    // 入退場アニメは View 実装側の責務 (Hide/Show の中でフェード等を行う想定)。
    public sealed class DefaultPortraitDirector : IPortraitDirector
    {
        private readonly IPortraitView _view;
        private readonly Dictionary<string, int> _cast = new();
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
            // 古い cast にあって新 cast に無いキャラは退場させる (View に Hide を指示)
            // 旧 layout の slot index を使う必要があるため、 layout を切替える前に Hide を発火する
            foreach (var oldEntry in _cast)
            {
                if (!cast.ContainsKey(oldEntry.Key))
                {
                    await _view.HideAsync(oldEntry.Value, ct);
                }
            }

            _cast.Clear();
            foreach (var entry in cast) _cast[entry.Key] = entry.Value;
            _layout = layout;
            _layoutInitialized = true;
            await _view.SwitchLayoutAsync(layout, ct);
        }

        public UniTask ShowAsync(string character, string portraitKey, CancellationToken ct)
        {
            if (!_layoutInitialized)
            {
                // stage 未宣言で portrait が呼ばれた: single レイアウトを暗黙適用して slot 0 にフォールバック (警告)
                Debug.LogWarning($"[Novel] portrait '{character}' が stage 宣言なしで呼ばれました。 " +
                                 "暗黙に single レイアウトで slot 0 にフォールバックします。");
                _layout = PortraitLayout.Single;
                _layoutInitialized = true;
            }
            var slotIndex = ResolveSlot(character);
            return _view.ShowAsync(slotIndex, character, portraitKey, ct);
        }

        public UniTask ExitAsync(string character, CancellationToken ct)
        {
            if (!_cast.TryGetValue(character, out var slotIndex)) return UniTask.CompletedTask;
            _cast.Remove(character);
            return _view.HideAsync(slotIndex, ct);
        }

        public async UniTask ClearStageAsync(CancellationToken ct)
        {
            foreach (var entry in _cast) await _view.HideAsync(entry.Value, ct);
            _cast.Clear();
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
