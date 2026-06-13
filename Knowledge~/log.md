# Update Log

更新履歴を新しい順に記録する。日付は `YYYY-MM-DD`。

## 2026-06-13
* **Creation**: 知識ベースを OKF v0.1 で初期化。okf-conventions を submodule 導入。
* **Creation**: [概要](/design/overview.md) - ライブラリの目的・前提スタック・調査対象・スコープ。
* **Creation**: [アーキテクチャ](/design/architecture.md) - 4 アセンブリ構成・コマンドバス・View 抽象・配線。
* **Creation**: [機能棚卸し](/design/feature-inventory.md) - 6 プロジェクト調査の機能マトリクスと統合機能案。
* **Creation**: 意思決定 8 件を記録（[実行モデル](/design/decisions/execution-model.md) / [ライブラリ範囲](/design/decisions/library-scope.md) / [インラインタグ](/design/decisions/inline-tags.md) / [DSL 語彙](/design/decisions/dsl-vocabulary.md) / [決定性コントラクト](/design/decisions/determinism-contract.md) / [キャラクターモデル](/design/decisions/character-model.md) / [ローカライズ](/design/decisions/localization.md) / [状態モデル](/design/decisions/state-model.md)）。
* **Creation**: [残論点](/design/open-questions.md) - 未決の設計判断（コマンドスキーマ versioning ほか）。
* **Creation**: [コマンド名規約と say スキーマ](/design/decisions/command-schema.md) を確定（最小 C# 層 + 糖衣、話者は id 基本のハイブリッド）。
* **Update**: [残論点](/design/open-questions.md) からコマンド名規約を解決済みとして除去。
