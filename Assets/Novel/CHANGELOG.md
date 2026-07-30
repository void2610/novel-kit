# Changelog

本パッケージの変更履歴。形式は [Keep a Changelog](https://keepachangelog.com/ja/1.1.0/)、
バージョンは [Semantic Versioning](https://semver.org/lang/ja/) に従う（安定版 1.0.0 までは破壊的変更があり得る）。

## [Unreleased]

### Changed
- **破壊的**: スプライトを扱うファセットの引数を `Sprite?` から `ResolvedSprite` (論理キー + 解決済みスプライト) へ変更。
  `IPortraitView` / `IBackgroundView` / `ICenterImageView` / `IPortraitDirector` が対象。ロードは引き続き runtime が行い
  View に解決の裁量はないが、未解決と消去の区別・同一キー再表示の no-op 判定・game 側の状態記録 (セーブからの背景復元、
  イベント CG の解放) はキーを要するため、キーを併せて渡す。
  移行: `ShowAsync(Sprite? sprite, ct)` を `ShowAsync(ResolvedSprite x, ct)` にして `x.Sprite` / `x.Key` を使う。
- **破壊的**: アセットのロード手段を抽象化。`IScenarioSource` / `IPreambleSource` / ルビ辞書の実装を
  `ScenarioSource` / `PreambleSource` / `RubyDictionary` に一本化し、ロード戦略は `ITextAssetLoader` を
  コンストラクタで明示指定する。`ResourcesScenarioSource` / `ResourcesPreambleSource` / `ResourcesRubyDictionary` は削除。
  移行: `new ScenarioSource(new ResourcesTextAssetLoader(), "Scenarios/")`。
- **破壊的**: スプライトのロードを追加 (`ISpriteLoader`) したうえで、解決を runtime 側へ集約。
  `IPortraitView` / `IBackgroundView` / `ICenterImageView` / `IPortraitDirector` の引数を論理キー (`string`) から
  解決済み `Sprite?` へ変更した。View は「渡された絵を表示する」だけになり、キー解決は `NovelCommandHandler` が行う。
  これらのファセットと `PortraitLayout` は名前空間が `Novel.Runtime` → `Novel.Assets` に移動。
  移行: View の `ShowAsync(string key, ct)` を `ShowAsync(Sprite? sprite, ct)` に変え、内部のロード処理を削除する。
- `ScenarioSource` / `PreambleSource` / `RubyDictionary` / `RubyMarkup` / `NovelDisplayText` を `Novel.View` から
  `Novel.Runtime` へ移設 (純 C# のため。自前 View 実装者が参考 View アセンブリに依存せずに使える)。
- **破壊的**: 辞書ルビの適用位置が runtime (`NovelCommandHandler`) に移動。`IRubyDictionary` を DI 登録するだけで
  say / choose の表示テキストに自動適用される (既読 ID とバックログには混入しない)。
  移行: View 側の `ApplyTo` 呼び出しを**削除すること**。残すと二重適用になり、1 回目の出力に含まれる
  `<noparse>` 内の親文字が再マッチしてオーバーレイ markup が noparse 区間へ注入され、TMP タグがリテラル表示される。
  なお「初出のみ」の周回リセット (`ResetShown()`) は引き続き game 所有 (runner が勝手に呼ぶとシナリオ単位で
  リセットされセマンティクスが壊れるため)。

### Added
- `ITextAssetLoader` (テキスト) と `ISpriteLoader` (スプライト) のロード抽象、および Resources / Addressables 実装。
  Addressables 実装は `Novel.Addressables` asmdef に隔離し、`com.unity.addressables` 導入時のみコンパイルされる。
- `NovelDisplayText.Build` を公開。自前 View が `TextRevealEngine` の可視文字数と整合する TMP 文字列構築を再実装せずに済む。

### Fixed
- `RegisterNovelKit` が PlayerLoop 停止中に `async UniTask` を同期待ちしてデッドロックしていた問題。
- 親文字なし / 未クローズの `<ruby=よみ>` でよみが表示から脱落していた問題。

## [0.1.0] - 2026-06-14

### Added
- 初期リリース（実装フェーズ）。ゲーム内ノベルパート向けの再利用可能ライブラリ。
- コア（`Novel.Runtime`・純 C#）: `NovelScenarioRunner`（`PlayAsync` で 1 シナリオ再生）、`NovelCommandHandler`、
  `INovelView` 抽象、行内タグ lexer `NovelTagLexer`、UI 非依存のタイプライタ進行エンジン `TextRevealEngine`（`IFrameClock` 駆動）、
  単一 `IStateStore`（フラグ/変数/既読）、`IScenarioSource`/`IPreambleSource`/`ISaveStore`/`ICharacterCatalog`/`ITextResolver`/
  `IWorldEffectSink`/`INovelErrorHandler` 抽象。
- コマンド語彙（`Novel.Commands`）: `say/choose/flag/portrait/bg/still/se/bgm/wait/world_effect`。
- preamble 糖衣（Ruby）: `say/narration/chara/flag/val/flag?/portrait/bg/still/se/bgm/wait/choose` +
  世界エフェクト（`world_effect/shake/flash/fade_out/fade_in/blackout`）。
- 参考 View（`Novel.View`）: TMP メッセージ窓 + 選択肢 + shake/wave 頂点アニメ + auto/skip の `NovelMessageView`、
  Resources ローダ、`ScriptableCharacterCatalog`、dev 警告ファセット、`DebugNovelErrorHandler`。
- DI 統合: `RegisterNovelKitCore()`（`Novel.VContainer`・コアのみ）/ `RegisterNovelKit()`（`Novel.View.VContainer`・箱出し）。
- Editor: シナリオ検証メニュー `Novel/Validate Scenarios`。
- 実行モデルは前進専用 + チェックポイント割り切り（セーブは `PlayAsync` 境界）。MRuby 実行エラーは backtrace つきで surface。

### Notes
- 設計判断は `Knowledge~/design/`（OKF 知識ベース）、設計との乖離監査は `implementation-review.md` を参照。
- 安定版前のため、既読ハッシュ等の内部形式は予告なく変わりうる（移行機構なし）。
