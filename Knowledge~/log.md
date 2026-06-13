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
* **Creation**: [ルーター所有権](/design/decisions/router-ownership.md) を確定（ノベル専用 Router を container 登録・ハンドラ DI 市民・世界エフェクトは明示ブリッジ既定なし）。
* **Update**: [残論点](/design/open-questions.md) のルーター所有権を解決済みに、世界エフェクト await 意味論を残論点として明確化。
* **Creation**: [音声スコープ](/design/decisions/audio-scope.md) を確定（voice は v1 除外で将来は別コマンド+糖衣、SE/BGM は v1 採用）。
* **Update**: [コマンド名規約と say スキーマ](/design/decisions/command-schema.md) から VoiceId slot を撤去（say は最小に確定）。
* **Creation**: [コマンドスキーマ versioning](/design/decisions/command-versioning.md) を確定（versioning 機構は持たない・.rb 正で再生成追従・save/replay 互換は別概念）。
* **Update**: [残論点](/design/open-questions.md) のコマンドスキーマ versioning を解決済みに、シナリオ内容 versioning を save の課題として明記。
* **Update**: [行内インラインタグ](/design/decisions/inline-tags.md) にタグ構文を確定（TMP `<...>` 単一・ライブラリ所有 lexer・非タグはエスケープ・パースは Runtime）。
* **Update**: [残論点](/design/open-questions.md) のインラインタグ構文を解決済みに（凍結前の高優先が全て解決）。
* **Creation**: [フロー/シーケンサの境界](/design/decisions/flow-boundary.md) を確定（ゲーム内ノベルパート前提・進行は完全 game 所有・シーケンサ/goto 無し・PlayAsync のみ）。
* **Update**: [概要](/design/overview.md) のスコープに「ゲーム内ノベルパート」を明記。[残論点](/design/open-questions.md) のフロー境界を解決済みに。
* **Creation**: [MRuby エラー処理・サンドボックス](/design/decisions/error-handling.md) を確定（backtrace surface・リリースは Faulted フェイルセーフ・サンドボックス v1 無し）。
* **Update**: [残論点](/design/open-questions.md) の MRuby エラー処理を解決済みに、UGC サンドボックスをバックログへ。
* **Creation**: [エフェクトの await 意味論](/design/decisions/effect-await.md) を確定（ハンドラ await で統一・IWorldEffectSink は async・per-call 上書きは v1 無し）。
* **Update**: [ルーター所有権](/design/decisions/router-ownership.md) の await 意味論を解決済みに相互参照。[残論点](/design/open-questions.md) のエフェクト await を解決済みに。
* **Creation**: [セーブのスナップショット粒度](/design/decisions/save-snapshot.md) を確定（永続は IStateStore のみ・セーブ境界は PlayAsync の間・途中保存は v1 対象外）。
* **Update**: [残論点](/design/open-questions.md) のセーブ粒度を解決済みに、途中再開（リプレイ式 save-anywhere）+ シナリオ内容 versioning をバックログへ。中優先が全て解決。
* **Update**: CG 鑑賞（ギャラリー/コレクション）を明確にスコープ外と確定。[概要](/design/overview.md) の対象外に「ゲーム全体メタ機能」を追記、[残論点](/design/open-questions.md) のバックログで「メッセージ窓 hide（ノベルパート内アフォーダンス）」と分離。
* **Update**: [アーキテクチャ](/design/architecture.md) を確定 15 ADR へ整合（API 凍結前の精査）。ルーター所有権の「未決・provider 案有力」記述を確定内容（ノベル専用 Router を container 登録・provider 抽象は入れない）へ修正。世界エフェクト（`IWorldEffectSink` async・明示ブリッジ）/ 実行結果（`NovelResult` completed/cancelled/faulted）/ エラー処理（`INovelErrorHandler`）の節を追加。セーブ境界・preamble の `bgm`・配線リストへ `IWorldEffectSink`/`INovelErrorHandler`/`ITextResolver` を反映。

## 2026-06-14
* **Creation**: [公開 API 表面（凍結）](/design/api-surface.md) - 確定 15 ADR を統合した公開シグネチャ（ランナー `PlayAsync`/`NovelResult`・`SayCommand`・`INovelView` + ファセット・game 供給サービス・ルーター所有権）を 1 箇所に集約。メンバ単位の細部（`se`/`bgm` 引数・v1 タグセット）は実装時確定と明記。[design/index](/design/index.md) に登録。
* **Update**: [アーキテクチャ](/design/architecture.md) の `Novel.Editor` 記述を実態へ修正。`.rb`→`.mrb` ScriptedImporter は mrubycs-compiler パッケージ提供のため再実装せず、Editor はカタログ/検証インスペクタに専念する（実装着手時に判明）。
* **Update**: [公開 API 表面](/design/api-surface.md) の `IScenarioSource` を `Irep` 返しから `.mrb` バイトコード（`byte[]`）返しへ修正。`Irep` パースは MRubyState 依存で runner 側が行うため（実装時に判明）。
