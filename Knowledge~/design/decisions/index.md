# 意思決定 (ADR)

`novel-kit` の設計判断を「なぜそう決めたか」とともに 1 件 1 ファイルで記録する。
各ファイルは `type: Decision`。ステータスは `確定` / `暫定` / `保留`。

## 確定済み（2026-06-13・ユーザーとの議論で合意）
* [実行モデル: 前進専用 + チェックポイント割り切り](/design/decisions/execution-model.md) - セーブは `PlayAsync` 境界のみ。履歴記録/save-anywhere/ロールバックは持たない（2026-06-14 にリプレイ前提から格下げ）
* [ライブラリ範囲: インターフェースコア + 参考 View 別パッケージ](/design/decisions/library-scope.md) - Runtime は純 C#、View は任意依存
* [インラインタグ: 行内タグ言語を v1 実装](/design/decisions/inline-tags.md) - 字句解析ベースのタイプライタ
* [DSL 語彙: リッチ統一語彙を常設](/design/decisions/dsl-vocabulary.md) - 未配線コマンドは no-op
* [決定性コントラクト: moot（リプレイ前提消失）](/design/decisions/determinism-contract.md) - 実行モデルが前進専用へ格下げされリプレイ自体が消えたため当面 moot。入力履歴の day1 記録は持たない
* [キャラクターモデル: 単一スプライト差し替え](/design/decisions/character-model.md) - 単一スロット
* [ローカライズ: 日本語のみ + 抽出フック](/design/decisions/localization.md) - `ITextResolver` で多言語は非破壊後付け
* [状態モデル: 単一 IStateStore に統一](/design/decisions/state-model.md) - `choose()` はユニークキー割当
* [コマンド名規約と say スキーマ](/design/decisions/command-schema.md) - 最小 C# 層 + 糖衣。話者は id 基本のハイブリッド
* [ルーター所有権](/design/decisions/router-ownership.md) - ノベル専用 Router を container 登録・ハンドラは DI 市民・世界エフェクトは明示ブリッジ
* [音声スコープ](/design/decisions/audio-scope.md) - voice は v1 除外（将来は別コマンド+糖衣）・SE/BGM は v1 採用
* [コマンドスキーマ versioning](/design/decisions/command-versioning.md) - versioning 機構は持たない（.rb 正・再生成で追従）・save/replay 互換は別概念
* [フロー/シーケンサの境界](/design/decisions/flow-boundary.md) - ゲーム内ノベルパート前提。進行は完全 game 所有・シーケンサ/goto 無し・PlayAsync のみ
* [MRuby エラー処理・サンドボックス](/design/decisions/error-handling.md) - try/catch で backtrace surface・リリースは Faulted でフェイルセーフ・サンドボックス v1 無し
* [エフェクトの await 意味論](/design/decisions/effect-await.md) - ハンドラ await で blocking/non-blocking 統一・IWorldEffectSink は async・per-call 上書きは v1 無し
* [セーブのスナップショット粒度](/design/decisions/save-snapshot.md) - 永続は IStateStore のみ・セーブ境界は PlayAsync の間・途中保存は v1 対象外
