# 意思決定 (ADR)

`novel-kit` の設計判断を「なぜそう決めたか」とともに 1 件 1 ファイルで記録する。
各ファイルは `type: Decision`。ステータスは `確定` / `暫定` / `保留`。

## 確定済み（2026-06-13・ユーザーとの議論で合意）
* [実行モデル: リプレイ前提設計](/design/decisions/execution-model.md) - 入力ログ + `.rb` 再実行。save-anywhere/ロールバックを非破壊で後付け可能に
* [ライブラリ範囲: インターフェースコア + 参考 View 別パッケージ](/design/decisions/library-scope.md) - Runtime は純 C#、View は任意依存
* [インラインタグ: 行内タグ言語を v1 実装](/design/decisions/inline-tags.md) - 字句解析ベースのタイプライタ
* [DSL 語彙: リッチ統一語彙を常設](/design/decisions/dsl-vocabulary.md) - 未配線コマンドは no-op
* [決定性コントラクト: 当面は後回し](/design/decisions/determinism-contract.md) - 履歴記録のみ先行
* [キャラクターモデル: 単一スプライト差し替え](/design/decisions/character-model.md) - 単一スロット
* [ローカライズ: 日本語のみ + 抽出フック](/design/decisions/localization.md) - `ITextResolver` で多言語は非破壊後付け
* [状態モデル: 単一 IStateStore に統一](/design/decisions/state-model.md) - `choose()` はユニークキー割当
* [コマンド名規約と say スキーマ](/design/decisions/command-schema.md) - 最小 C# 層 + 糖衣。話者は id 基本のハイブリッド
* [ルーター所有権](/design/decisions/router-ownership.md) - ノベル専用 Router を container 登録・ハンドラは DI 市民・世界エフェクトは明示ブリッジ
