# Changelog

本パッケージの変更履歴。形式は [Keep a Changelog](https://keepachangelog.com/ja/1.1.0/)、
バージョンは [Semantic Versioning](https://semver.org/lang/ja/) に従う（安定版 1.0.0 までは破壊的変更があり得る）。

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
