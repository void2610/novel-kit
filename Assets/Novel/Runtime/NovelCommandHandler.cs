#nullable enable
using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using Novel.Commands;
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
        private readonly IPortraitView? _portrait;
        private readonly IBackgroundView? _background;
        private readonly IAudioChannel? _audio;
        private readonly IWorldEffectSink? _worldEffectSink;
        private readonly IBacklog? _backlog;

        public NovelCommandHandler(INovelView view, IStateStore state, ITextResolver text, ICharacterCatalog catalog,
            IPortraitView? portrait = null, IBackgroundView? background = null, IAudioChannel? audio = null,
            IWorldEffectSink? worldEffectSink = null, IBacklog? backlog = null)
        {
            _view = view;
            _state = state;
            _text = text;
            _catalog = catalog;
            _portrait = portrait;
            _background = background;
            _audio = audio;
            _worldEffectSink = worldEffectSink;
            _backlog = backlog;
        }

        public async UniTask On(SayCommand cmd, CancellationToken ct)
        {
            // PortraitKey が同時指定されていればここで切替（display_as で表示名を変えつつ、同一 speaker_id の立ち絵を 1 行で指定する糖衣）
            if (!string.IsNullOrEmpty(cmd.PortraitKey) && _portrait != null)
                await _portrait.ShowAsync(cmd.SpeakerId, cmd.PortraitKey, ct);

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
            if (_portrait != null) await _portrait.ShowAsync(cmd.Character, cmd.PortraitKey, ct);
        }

        public async UniTask On(BackgroundCommand cmd, CancellationToken ct)
        {
            if (_background != null) await _background.ShowAsync(cmd.BackgroundKey, ct);
        }

        public async UniTask On(StillCommand cmd, CancellationToken ct)
        {
            if (_background != null) await _background.ShowStillAsync(cmd.StillKey, ct);
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
