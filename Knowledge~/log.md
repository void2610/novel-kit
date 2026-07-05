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
* **Update**: UPM 配布準備を整え **0.1.0** をタグ付け。`package.json`（`com.void2610.novel-kit`）/ CHANGELOG / MIT `LICENSE.md` を追加し、混成依存（Git UPM 6本 + NuGet 4本）の前提条件を [README](/../Assets/Novel/README.md) に明記。Samples を `Samples~/Basic`（UPM opt-in サンプル）へ移動。Unity 実検証: コンパイル 0 エラー/0 警告・EditMode **17/17** 緑。[getting-started](/../Docs/getting-started.md) のサンプル参照を更新。
* **Update**: ドキュメント群を as-built 実装へ追随（並行監査で 13 箇所修正）。[CLAUDE.md] フェーズ/アセンブリ一覧、[README] コマンド表/Editor、[アーキテクチャ](/design/architecture.md) 6 アセンブリ + `Novel.View.VContainer` 追記、[公開 API 表面](/design/api-surface.md) `ITextResolver` 適用範囲・preamble 糖衣（chara/val/flag?/world_effect 系）、[概要](/design/overview.md)・[decisions/index](/design/decisions/index.md) の決定性コントラクト moot 化、[残論点](/design/open-questions.md) の Editor バリデータ実装済み注記。
* **Update**: DI ヘルパの View/Resources 結合を解消（`NK-VC-VIEW-COUPLE`）。`Novel.VContainer` をコア専用（`RegisterNovelKitCore`・純 Runtime 依存）にし、参考 View + Resources ローダ + 警告/ログ既定は新規 asmdef `Novel.View.VContainer` の `RegisterNovelKit` へ分離。`Novel.VContainer.asmdef` から `Novel.View` 参照を除去。[ライブラリ範囲](/design/decisions/library-scope.md) に追認。既存挙動は不変。
* **Update**: [実行モデル](/design/decisions/execution-model.md) を「リプレイ前提設計」から **「前進専用 + チェックポイント割り切り」へ格下げ**（`NK-REPLAY-FOUND` の決着）。入力履歴 day1 記録・save-anywhere・ロールバックは v1 で持たないと確定し、将来やるなら一括（実質作り直し）と明記。下流を整合: [セーブ粒度](/design/decisions/save-snapshot.md)（履歴維持を撤回）/ [決定性コントラクト](/design/decisions/determinism-contract.md)（moot 化）/ [コマンド versioning](/design/decisions/command-versioning.md) / [概要](/design/overview.md) / [アーキテクチャ](/design/architecture.md) / [残論点](/design/open-questions.md)。現実装（境界セーブ）が既にこの形のためコード変更なし。
* **Update**: [実装レビュー](/design/implementation-review.md) の残課題（低優先）のうち設計判断不要分を追加実装。`NK-REENTRANCY`（`PlayAsync` 再入を fail-fast + テスト追加）/ `NK-STABLEID`（既読ハッシュ 32→64bit FNV）/ `NK-SCENSRC-DRIFT`（空キーガード + `EndsWith` Ordinal 化 + [architecture](/design/architecture.md) のシナリオソース記述を実装へ整合）。`NK-NOPARSE-INDEX` は検証で非問題と判断。残る `NK-VC-VIEW-COUPLE`/`NK-READ-GROWTH`/`NK-REPLAY-FOUND` は要設計判断として保留。
* **Update**: 実装レビュー（[implementation-review](/design/implementation-review.md)）の P0-P2 を実装し ADR を追認。
  - **P0** `NK-TYPEWRITER-IN-VIEW`/`NK-SPAN-NEST`/`NK-WAIT-CLICK`: 進行エンジン `TextRevealEngine` + `IFrameClock` を Runtime に新設し View を薄い TMP アダプタ化。区間スタック対応・`<w>` 中クリック対応・ヘッドレステスト追加 → [インラインタグ](/design/decisions/inline-tags.md) 追認。
  - **P1** `NK-ERROR-SILENT`: `NovelErrorInfo` + 既定 `DebugNovelErrorHandler`（Ruby backtrace を surface）→ [エラー処理](/design/decisions/error-handling.md) 追認。`NK-WORLDFX-DEAD`: `WorldEffectCommand` + handler ブリッジ + preamble 糖衣 → [エフェクト await](/design/decisions/effect-await.md) 追認。`NK-NOOP-SILENT`: dev 警告 no-op ファセット → [DSL 語彙](/design/decisions/dsl-vocabulary.md) 追認。`NK-CHARNAME-SUGAR`: キャラ名糖衣を preamble の `chara` ヘルパ（`define_method` 方式・登場キャラ分だけ `chara :alice` と書く）で実装 → [コマンド名規約](/design/decisions/command-schema.md) 追認。`method_missing` は MRubyCS 0.19.2 未対応（`PrepareMethodMissing` で NRE）のため不採用。
  - **P2** `NK-PREAMBLE-RACE`（成功時のみフラグ）/ `NK-CHOOSE-KEY`+`NK-STATE-NS`（`__` 始まりを Capture 除外 + choose 明示キー）→ [状態モデル](/design/decisions/state-model.md) 追認 / `NK-RESOLVE-CHOICES`（`ITextResolver` を choose・表示名へ）/ `NK-TMP-WHITELIST`（TMP タグ補完）/ `NK-VAR-READ-SUGAR`（`val`/`flag?`）。
* **Update**: [Getting Started](/../Docs/getting-started.md) を新機能（キャラ名糖衣・世界エフェクト・read 糖衣・明示 choose key・エラー可視化・自前 View からの `TextRevealEngine` 駆動）に更新。
* **Creation**: [実装レビュー（設計との乖離監査）](/design/implementation-review.md) - 確定 16 ADR と実装を突き合わせた批判的レビュー（多角レビュー + 敵対的検証で確認 40 / 棄却 7）。乖離・正当性バグ・設計疑問に追跡用 ID を付与し優先度付き改善ロードマップ（P0-P3）を記録。[design/index](/design/index.md) に登録。
* **Creation**: [公開 API 表面（凍結）](/design/api-surface.md) - 確定 15 ADR を統合した公開シグネチャ（ランナー `PlayAsync`/`NovelResult`・`SayCommand`・`INovelView` + ファセット・game 供給サービス・ルーター所有権）を 1 箇所に集約。メンバ単位の細部（`se`/`bgm` 引数・v1 タグセット）は実装時確定と明記。[design/index](/design/index.md) に登録。
* **Update**: [アーキテクチャ](/design/architecture.md) の `Novel.Editor` 記述を実態へ修正。`.rb`→`.mrb` ScriptedImporter は mrubycs-compiler パッケージ提供のため再実装せず、Editor はカタログ/検証インスペクタに専念する（実装着手時に判明）。
* **Update**: [公開 API 表面](/design/api-surface.md) の `IScenarioSource` を `Irep` 返しから `.mrb` バイトコード（`byte[]`）返しへ修正。`Irep` パースは MRubyState 依存で runner 側が行うため（実装時に判明）。
* **Update**: 実装で生じた判断を確定 ADR へ追認し fix-later を解消。[ルーター所有権](/design/decisions/router-ownership.md) に「handler は runner が MapTo」、[状態モデル](/design/decisions/state-model.md) に「`IStateStore` は runtime 内部 `MRubyStateStore` が既定・永続は `ISaveStore`」を追記。[残論点](/design/open-questions.md) の該当節を解決済みへ。
* **Update**: プロジェクト独自コマンドの拡張口 `INovelCommandModule` を追加（`NK-PROJECT-CMD`）。color-recollection 導入検討で発覚した「game 固有コマンドを定義できない」隘路を解消。語彙束縛 + ハンドラ写像を 1 クラスに束ね、runner は `IEnumerable<INovelCommandModule>` / `IEnumerable<IPreambleSource>`（糖衣の積み増し）を集約注入。VContainer は `RegisterNovelCommand<TModule>()` 1 行。EditMode 18/18 緑。[DSL 語彙](/design/decisions/dsl-vocabulary.md)・[API 表面](/design/api-surface.md)・[実装レビュー](/design/implementation-review.md) を追随。
* **Update**: バックログ（A-1）を実装。`Novel.Runtime` に `IBacklog`/`BacklogEntry`/`RingBufferBacklog`（rich 保持・既定 200 行）を追加し、`NovelCommandHandler` が say 表示ごとに話者+rich 本文を追記、`RegisterNovelKitCore` が既定登録。閲覧 UI / Clear 契機は game 所有。color-recollection の `Backlog.cs` 相当をライブラリへ移管。[API 表面](/design/api-surface.md)・[アーキテクチャ](/design/architecture.md) に追認。
* **Update**: ルビ展開（A-2）を実装。[インラインタグ ADR](/design/decisions/inline-tags.md) が約束した `<ruby=よみ>漢字</ruby>` → TMP 展開を、lexer の `Ignored` 格下げから `RubyPush`/`RubyPop` トークンへ昇格。`TextRevealEngine` がよみを可視数に算入し、`Novel.View` の `RubyMarkup.BuildOverlay`（座標トリック）が展開。`ToPlainText` はよみを除外（既読ハッシュ/バックログ平文）。辞書ルビ・`:first` 等の JP 固有部は game 任意モジュール（CR の `RubyDictionary` が `<ruby>` 挿入側を担う分業）。EditMode 25/25 緑。
* **Update**: セーブの JSON 直列化をライブラリ所有へ。[セーブ粒度](/design/decisions/save-snapshot.md) の当初帰結「ライブラリはセーブ形式を持たず game 所有」を改め、`Novel.Runtime` に純 C#(UnityEngine 非依存)の `NovelSaveSerializer`(`NovelStateSnapshot` ⇔ JSON・決定的出力・破損耐性)を追加。IO は `INovelSaveBlobStore` に委譲し `JsonSaveStore : ISaveStore` が束ねる。Unity 実装 `PlayerPrefsSaveBlobStore`(Novel.View)+ DI `RegisterNovelJsonSave<TBlob>()` を同梱。粒度/境界(永続は IStateStore の Capture・境界は PlayAsync の狭間)は不変で、変えたのは形式の所有者のみ。color-recollection のセーブ実装(フラグ/既読の game 側直列化)を参考にライブラリへ移管。EditMode `NovelSaveSerializerTests` 追加(Unity 未起動の本環境ではテスト未実行)。
