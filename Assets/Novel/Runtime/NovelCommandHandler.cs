#nullable enable
using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using Novel.Commands;
using UnityEngine;
using VitalRouter;

namespace Novel.Runtime
{
    // ノベル専用 Router にマップされる [Routes] ハンドラ。ハンドラ await で進行が成立する（Fiber サスペンション）。
    // 未供給のファセット（portrait/bg/audio/worldEffect）は no-op（dsl-vocabulary: 未配線は握りつぶす）
    [Routes]
    public partial class NovelCommandHandler
    {
        private readonly INovelView _view;
        private readonly IStateStore _state;
        private readonly ITextResolver _text;
        private readonly ICharacterCatalog _catalog;
        private readonly IPortraitDirector? _portraitDirector;
        private readonly IBackgroundView? _background;
        private readonly ICenterImageView? _centerImage;
        private readonly IAudioChannel? _audio;
        private readonly IWorldEffectSink? _worldEffectSink;
        private readonly IBacklog? _backlog;

        public NovelCommandHandler(INovelView view, IStateStore state, ITextResolver text, ICharacterCatalog catalog,
            IPortraitDirector? portraitDirector = null, IBackgroundView? background = null, IAudioChannel? audio = null,
            IWorldEffectSink? worldEffectSink = null, IBacklog? backlog = null,
            ICenterImageView? centerImage = null)
        {
            _view = view;
            _state = state;
            _text = text;
            _catalog = catalog;
            _portraitDirector = portraitDirector;
            _background = background;
            _centerImage = centerImage;
            _audio = audio;
            _worldEffectSink = worldEffectSink;
            _backlog = backlog;
        }

        public async UniTask On(SayCommand cmd, CancellationToken ct)
        {
            // PortraitKey が同時指定されていればここで切替（display_as で表示名を変えつつ、同一 speaker_id の立ち絵を 1 行で指定する糖衣）
            if (!string.IsNullOrEmpty(cmd.PortraitKey) && _portraitDirector != null)
                await _portraitDirector.ShowAsync(cmd.SpeakerId, cmd.PortraitKey, ct);

            var resolved = _text.Resolve(cmd.Text);
            var displayName = ResolveDisplayName(cmd);
            if (displayName != null) displayName = _text.Resolve(displayName);   // 表示名も多言語 seam を通す（localization）
            // 既読 ID はタグを除いた素テキストで算出（タグ有無で既読が割れないように）
            var textId = StableId.Of(cmd.SpeakerId, NovelTagLexer.ToPlainText(resolved));
            var alreadyRead = _state.IsRead(textId);

            // バックログは rich のまま記録（link/color を残し再表示・キーワード収集できるように。Clear 契機は game 所有）
            _backlog?.Add(displayName ?? "", resolved);

            // Text はタグ付き原文を渡し、View 側 typewriter が NovelTagLexer で逐次 Reveal する
            await _view.ShowMessageAsync(new NovelLine(cmd.SpeakerId, displayName, resolved, alreadyRead), ct);

            _state.MarkRead(textId);
        }

        // 選択 → index を共有テーブル経由で StateKey に書く（Ruby の state[:key] が読む）
        public async UniTask On(ChooseCommand cmd, CancellationToken ct)
        {
            // 選択肢も say と同じく ITextResolver を通す（多言語化の seam を say と揃える）
            var options = cmd.Options;
            var resolved = new string[options.Length];
            for (int i = 0; i < options.Length; i++) resolved[i] = _text.Resolve(options[i]);

            var selected = await _view.ShowChoicesAsync(resolved, ct);
            _state.Set(cmd.StateKey, selected);
        }

        public void On(FlagCommand cmd) => _state.Set(cmd.Key, cmd.Value);

        public async UniTask On(PortraitCommand cmd, CancellationToken ct)
        {
            if (_portraitDirector != null) await _portraitDirector.ShowAsync(cmd.Character, cmd.PortraitKey, ct);
        }

        // stage 宣言: layout と cast (キャラ → slot index) を Director に適用する。
        // CastPairs は [character0, index0, character1, index1, ...] のフラット配列 (Vocabulary コメント参照)。
        // DSL ミスを検出しやすくするため、 奇数要素 / 空 character / 負 slot index は警告 + skip する。
        public async UniTask On(StageCommand cmd, CancellationToken ct)
        {
            if (_portraitDirector == null) return;
            var pairs = cmd.CastPairs ?? Array.Empty<string>();
            if (pairs.Length % 2 != 0)
            {
                Debug.LogWarning($"[Novel] stage の cast_pairs の要素数が奇数 ({pairs.Length}) です。 末尾の半端な要素 '{pairs[pairs.Length - 1]}' は無視します。");
            }
            var cast = new Dictionary<string, int>(pairs.Length / 2);
            for (var i = 0; i + 1 < pairs.Length; i += 2)
            {
                var character = pairs[i];
                if (string.IsNullOrEmpty(character))
                {
                    Debug.LogWarning($"[Novel] stage cast に空文字 character は登録できません (index {i})。 スキップします。");
                    continue;
                }
                if (!int.TryParse(pairs[i + 1], out var slotIndex))
                {
                    Debug.LogWarning($"[Novel] stage cast の slot index が int に変換できません ({character}={pairs[i + 1]})。 スキップします。");
                    continue;
                }
                if (slotIndex < 0)
                {
                    Debug.LogWarning($"[Novel] stage cast の slot index に負値は使えません ({character}={slotIndex})。 スキップします。");
                    continue;
                }
                cast[character] = slotIndex;
            }
            await _portraitDirector.StageAsync(new PortraitLayout(cmd.LayoutId), cast, ct);
        }

        public async UniTask On(ExitCommand cmd, CancellationToken ct)
        {
            if (_portraitDirector != null) await _portraitDirector.ExitAsync(cmd.Character, ct);
        }

        public async UniTask On(ClearStageCommand cmd, CancellationToken ct)
        {
            if (_portraitDirector != null) await _portraitDirector.ClearStageAsync(ct);
        }

        public async UniTask On(BackgroundCommand cmd, CancellationToken ct)
        {
            if (_background != null) await _background.ShowAsync(cmd.BackgroundKey, ct);
        }

        public async UniTask On(StillCommand cmd, CancellationToken ct)
        {
            if (_background != null) await _background.ShowStillAsync(cmd.StillKey, ct);
        }

        public async UniTask On(CenterImageCommand cmd, CancellationToken ct)
        {
            // 空キー (image(nil) 等) は無効。消去は hide_image の責務なので no-op にする
            if (_centerImage != null && !string.IsNullOrEmpty(cmd.ImageKey))
                await _centerImage.ShowAsync(cmd.ImageKey, ct);
        }

        public async UniTask On(HideCenterImageCommand cmd, CancellationToken ct)
        {
            if (_centerImage != null) await _centerImage.HideAsync(ct);
        }

        public async UniTask On(SeCommand cmd, CancellationToken ct)
        {
            if (_audio != null) await _audio.PlaySeAsync(cmd.SeKey, ct);
        }

        // bgm は非ブロッキング（即 return）。空文字は停止
        public void On(BgmCommand cmd)
        {
            if (_audio == null) return;
            if (string.IsNullOrEmpty(cmd.BgmKey)) _audio.StopBgm();
            else _audio.PlayBgm(cmd.BgmKey);
        }

        public async UniTask On(WaitCommand cmd, CancellationToken ct)
        {
            await UniTask.Delay(TimeSpan.FromSeconds(cmd.Seconds), cancellationToken: ct);
        }

        // 世界エフェクト（カメラ/画面/gameplay への脱出）。常に await し、blocking/non-blocking は sink が返すタスクで決まる
        // （非ブロッキング=即完了タスク / ブロッキング=完了時解決タスク。effect-await ADR）。未供給なら no-op
        public async UniTask On(WorldEffectCommand cmd, CancellationToken ct)
        {
            if (_worldEffectSink == null) return;
            await _worldEffectSink.DispatchAsync(new WorldEffect(cmd.EffectKey, cmd.Args ?? Array.Empty<float>()), ct);
        }

        public void On(MessageWindowVisibilityCommand cmd) => _view.SetMessageWindowVisible(cmd.Visible);

        // command-schema の解決 3 規則: 空=ナレーション / カタログ有=表示名（DisplayAs で上書き）/ 未登録=id をそのまま
        private string? ResolveDisplayName(SayCommand cmd)
        {
            if (string.IsNullOrEmpty(cmd.SpeakerId)) return null;
            if (cmd.DisplayAs is { } overrideName) return overrideName;
            if (_catalog.TryGet(cmd.SpeakerId, out var entry)) return entry.DisplayName;
            return cmd.SpeakerId;
        }
    }
}
