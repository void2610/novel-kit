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

        public NovelCommandHandler(INovelView view, IStateStore state, ITextResolver text, ICharacterCatalog catalog,
            IPortraitView? portrait = null, IBackgroundView? background = null, IAudioChannel? audio = null,
            IWorldEffectSink? worldEffectSink = null)
        {
            _view = view;
            _state = state;
            _text = text;
            _catalog = catalog;
            _portrait = portrait;
            _background = background;
            _audio = audio;
            _worldEffectSink = worldEffectSink;
        }

        public async UniTask On(SayCommand cmd, CancellationToken ct)
        {
            var resolved = _text.Resolve(cmd.Text);
            var displayName = ResolveDisplayName(cmd);
            // 既読 ID はタグを除いた素テキストで算出（タグ有無で既読が割れないように）
            var textId = StableId.Of(cmd.SpeakerId, NovelTagLexer.ToPlainText(resolved));
            var alreadyRead = _state.IsRead(textId);

            // Text はタグ付き原文を渡し、View 側 typewriter が NovelTagLexer で逐次 Reveal する
            await _view.ShowMessageAsync(new NovelLine(cmd.SpeakerId, displayName, resolved, alreadyRead), ct);

            _state.MarkRead(textId);
        }

        // 選択 → index を共有テーブル経由で StateKey に書く（Ruby の state[:key] が読む）
        public async UniTask On(ChooseCommand cmd, CancellationToken ct)
        {
            var selected = await _view.ShowChoicesAsync(cmd.Options, ct);
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
