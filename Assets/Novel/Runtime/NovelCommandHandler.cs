#nullable enable
using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using Novel.Assets;
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
        private readonly IBackgroundChannel? _background;
        private readonly IStillChannel? _still;
        private readonly ICenterImageChannel? _centerImage;
        private readonly IAudioChannel? _audio;
        private readonly IWorldEffectSink? _worldEffectSink;
        private readonly IBacklog? _backlog;
        private readonly ISpriteLoader? _sprites;
        private readonly IRubyDictionary? _ruby;
        private readonly NovelPlaybackProgress _progress;
        private readonly Func<string, string?> _variableLookup;
        private readonly Action<string> _onMissingVariable;
        private readonly Action<string>? _onSpriteNotFound;

        public NovelCommandHandler(INovelView view, IStateStore state, ITextResolver text, ICharacterCatalog catalog,
            IPortraitDirector? portraitDirector = null, IBackgroundChannel? background = null, IStillChannel? still = null,
            IAudioChannel? audio = null,
            IWorldEffectSink? worldEffectSink = null, IBacklog? backlog = null,
            ICenterImageChannel? centerImage = null, NovelPlaybackProgress? progress = null,
            ISpriteLoader? sprites = null, IRubyDictionary? ruby = null,
            ITextVariableProvider? textVariables = null, Action<string>? onSpriteNotFound = null)
        {
            _onSpriteNotFound = onSpriteNotFound;
            _view = view;
            _state = state;
            _text = text;
            _catalog = catalog;
            _portraitDirector = portraitDirector;
            _background = background;
            _still = still;
            _centerImage = centerImage;
            _audio = audio;
            _worldEffectSink = worldEffectSink;
            _backlog = backlog;
            _sprites = sprites;
            _ruby = ruby;
            _progress = progress ?? new NovelPlaybackProgress();
            // テキスト変数 %{key} の解決: game 供給 provider 優先 → IStateStore 変数値。デリゲートは行ごとの割当を避けるためここで固定
            _variableLookup = NovelTextVariables.CreateLookup(textVariables, state);
            _onMissingVariable = name =>
            {
                // dev 警告 (ADR)。本番はログを汚さない — プレースホルダ温存で画面上の可視性は保たれる
                if (Debug.isDebugBuild)
                    Debug.LogWarning(
                        $"[Novel] 未定義のテキスト変数 %{{{name}}} をそのまま表示します。flag/val の設定漏れか、ITextVariableProvider の未登録を確認してください。");
            };
        }

        public async UniTask On(SayCommand cmd, CancellationToken ct)
        {
            // 早送り中は表示待ちだけ省く。立ち絵/バックログ/既読は実行し、復帰地点の盤面を再構築する
            var fastForward = _progress.AdvanceSay(cmd.Text);

            // PortraitKey が同時指定されていればここで切替（display_as で表示名を変えつつ、同一 speaker_id の立ち絵を 1 行で指定する糖衣）。
            // 未指定なら catalog の既定立ち絵へフォールバックし、話者が喋るたびにその人の絵が出る
            var portraitKey = ResolvePortraitKey(cmd);
            // 表示中ならスプライトのロードごと省く (同一話者が連続で喋る間、 毎行ロードが走らないように)
            if (!string.IsNullOrEmpty(portraitKey) && _portraitDirector != null &&
                !_portraitDirector.IsShowing(cmd.SpeakerId, portraitKey))
                await _portraitDirector.ShowAsync(cmd.SpeakerId, await LoadSpriteAsync(portraitKey!, ct), ct);

            // テキスト変数 %{key} は訳の取得 (Resolve) 後に展開する (テンプレートがテーブルのキーで、
            // 翻訳者は訳文中でプレースホルダを自由に動かせる)
            var resolved = NovelTextVariables.Expand(_text.Resolve(cmd.Text), _variableLookup, _onMissingVariable);
            var displayName = ResolveDisplayName(cmd);
            if (displayName != null)   // 表示名も多言語 seam + 変数展開を通す（localization）
                displayName = NovelTextVariables.Expand(_text.Resolve(displayName), _variableLookup, _onMissingVariable);
            // 既読 ID は resolve/展開前の原文 (テンプレート) から算出する。ロケール切替で既読が分断せず、
            // %{gold} 等の値が変わっても同じ行は既読のまま (スキップが効く)。恒等 resolver では従来と同一ハッシュ。
            // タグは除いて算出 (タグ有無で既読が割れないように)
            var rawPlain = NovelTagLexer.ToPlainText(cmd.Text);
            var textId = StableId.Of(cmd.SpeakerId, rawPlain);
            var alreadyRead = _state.IsRead(textId);
            // View へ渡す平文は表示テキスト (resolve + 展開後) 基準。恒等 resolver + 変数無しは同一参照を返すため再計算を省く
            var plain = ReferenceEquals(resolved, cmd.Text) ? rawPlain : NovelTagLexer.ToPlainText(resolved);

            // バックログは rich のまま記録（link/color を残し再表示・キーワード収集できるように。Clear 契機は game 所有）
            _backlog?.Add(displayName ?? "", resolved);

            // 辞書ルビは表示専用: 既読 ID とバックログの確定後に付けることで、ふりがなが平文/既読 ID に混入しない。
            // 早送り中も適用する (初出ルビを消費させ、途中復帰後の初回プレイで既出の初出ルビが再表示されるのを防ぐ)
            var display = _ruby != null ? _ruby.ApplyTo(resolved) : resolved;

            // Text はタグ付き原文を渡し、View 側 typewriter が NovelTagLexer で逐次 Reveal する
            if (!fastForward) await _view.ShowMessageAsync(new NovelLine(cmd.SpeakerId, displayName, display, plain, alreadyRead), ct);

            _state.MarkRead(textId);
        }

        // 選択 → index を共有テーブル経由で StateKey に書く（Ruby の state[:key] が読む）
        public async UniTask On(ChooseCommand cmd, CancellationToken ct)
        {
            // 早送り中で選択結果が復元済みならUIを出さず前回の選択を保つ（未復元の自動採番キー等は通常表示に落とす）
            if (_progress.IsFastForwarding && _state.Has(cmd.StateKey)) return;

            // 選択肢も say と同じく ITextResolver + テキスト変数展開を通す（多言語化の seam を say と揃える）
            var options = cmd.Options;
            var resolved = new string[options.Length];
            for (int i = 0; i < options.Length; i++)
            {
                resolved[i] = NovelTextVariables.Expand(_text.Resolve(options[i]), _variableLookup, _onMissingVariable);
                // 選択肢も say と同じく表示専用の辞書ルビを付ける (適用位置の裁量を View に残さない)
                if (_ruby != null) resolved[i] = _ruby.ApplyTo(resolved[i]);
            }

            var selected = await _view.ShowChoicesAsync(resolved, ct);
            _state.Set(cmd.StateKey, selected);
        }

        public void On(FlagCommand cmd) => _state.Set(cmd.Key, cmd.Value);

        public async UniTask On(PortraitCommand cmd, CancellationToken ct)
        {
            if (_portraitDirector != null)
                await _portraitDirector.ShowAsync(cmd.Character, await LoadSpriteAsync(cmd.PortraitKey, ct), ct);
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
            if (_background != null) await _background.ShowAsync(await LoadSpriteAsync(cmd.BackgroundKey, ct), ct);
        }

        public async UniTask On(StillCommand cmd, CancellationToken ct)
        {
            if (_still != null) await _still.ShowAsync(await LoadSpriteAsync(cmd.StillKey, ct), ct);
        }

        public async UniTask On(CenterImageCommand cmd, CancellationToken ct)
        {
            // 空キー (image(nil) 等) は無効。消去は hide_image の責務なので no-op にする
            if (_centerImage != null && !string.IsNullOrEmpty(cmd.ImageKey))
                await _centerImage.ShowAsync(await LoadSpriteAsync(cmd.ImageKey, ct), ct);
        }

        public async UniTask On(HideCenterImageCommand cmd, CancellationToken ct)
        {
            if (_centerImage != null) await _centerImage.HideAsync(ct);
        }

        public async UniTask On(SeCommand cmd, CancellationToken ct)
        {
            if (_progress.IsFastForwarding) return; // 早送り中の効果音は復帰後に意味を持たないため鳴らさない
            if (_audio != null) await _audio.PlaySeAsync(cmd.SeKey, ct);
        }

        public async UniTask On(SeLoopCommand cmd, CancellationToken ct)
        {
            if (_progress.IsFastForwarding) return;
            if (_audio != null) await _audio.PlaySeLoopAsync(cmd.SeKey, cmd.Interval, cmd.Count, ct);
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
            if (_progress.IsFastForwarding) return; // 早送りは実時間を消費しない
            await UniTask.Delay(TimeSpan.FromSeconds(cmd.Seconds), cancellationToken: ct);
        }

        // 世界エフェクト（カメラ/画面/gameplay への脱出）。常に await し、blocking/non-blocking は sink が返すタスクで決まる
        // （非ブロッキング=即完了タスク / ブロッキング=完了時解決タスク。effect-await ADR）。未供給なら no-op
        public async UniTask On(WorldEffectCommand cmd, CancellationToken ct)
        {
            if (_progress.IsFastForwarding) return; // 演出 (shake/flash 等) は瞬間表現なので早送りでは再現しない
            if (_worldEffectSink == null) return;
            await _worldEffectSink.DispatchAsync(new WorldEffect(cmd.EffectKey, cmd.Args ?? Array.Empty<float>()), ct);
        }

        public void On(MessageWindowVisibilityCommand cmd) => _view.SetMessageWindowVisible(cmd.Visible);

        public void On(ClearMessageCommand cmd) => _view.ClearMessage();

        // 既定立ち絵の適用は stage cast 在籍の話者に限る。
        // cast 外 (clear_stage 後の回想・夢シーン等) で出すと、居ないはずのキャラが喋るたびに現れてしまう
        private string? ResolvePortraitKey(SayCommand cmd)
        {
            if (!string.IsNullOrEmpty(cmd.PortraitKey)) return cmd.PortraitKey;
            if (string.IsNullOrEmpty(cmd.SpeakerId) || _portraitDirector == null) return null;
            if (!_portraitDirector.IsStaged(cmd.SpeakerId)) return null;
            // 既に出ている立ち絵は尊重する。 上書きすると portrait コマンドの指定が次の say で毎回消える
            if (_portraitDirector.HasPortrait(cmd.SpeakerId)) return null;
            return _catalog.TryGet(cmd.SpeakerId, out var entry) ? entry.DefaultPortraitKey : null;
        }

        // command-schema の解決 3 規則: 空=ナレーション / カタログ有=表示名（DisplayAs で上書き）/ 未登録=id をそのまま
        // 空キー (bg nil 等の消去) はロード対象でないためローダーへ渡さない。ローダー未供給なら null のまま返す
        private async UniTask<ResolvedSprite> LoadSpriteAsync(string key, CancellationToken ct)
        {
            if (string.IsNullOrEmpty(key) || _sprites == null) return new ResolvedSprite(key, null);
            var sprite = await _sprites.LoadAsync(key, ct);
            // ローダがあるのに引けないキーは誤記か未配置。ローダ未供給は別問題 (未供給ファセット警告の領域) なので報告しない
            if (sprite == null) _onSpriteNotFound?.Invoke(key);
            return new ResolvedSprite(key, sprite);
        }

        private string? ResolveDisplayName(SayCommand cmd)
        {
            if (string.IsNullOrEmpty(cmd.SpeakerId)) return null;
            if (cmd.DisplayAs is { } overrideName) return overrideName;
            if (_catalog.TryGet(cmd.SpeakerId, out var entry)) return entry.DisplayName;
            return cmd.SpeakerId;
        }
    }
}
