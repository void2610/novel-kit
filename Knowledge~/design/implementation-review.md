---
type: Design
title: 実装レビュー（設計との乖離監査）
description: 確定 16 ADR と実装を突き合わせた批判的レビュー。多角レビュー + 敵対的検証で確認した乖離・バグ・設計疑問と、優先度付き改善ロードマップ。
tags: [review, audit, drift, findings, roadmap]
timestamp: 2026-06-15T00:00:00Z
status: 実施（P0-P2 + 残課題の安全分 実装済み・ADR 追認済み）
---

# 位置づけ

実装フェーズ初期（[CLAUDE.md] 記載のとおり API 流動）における、**3 評価基準**に照らした批判的レビューの記録。

評価基準:
1. **ライブラリの性質** — スタンドアロン VN ではなく「ゲーム内の一要素としてのノベルパート」を組み込むエンジン（[概要](/design/overview.md) / [フロー境界](/design/decisions/flow-boundary.md)）。
2. **拡張性と柔軟性** — 複数プロジェクトで View を入れ替える前提。その切り替えが容易か（[ライブラリ範囲](/design/decisions/library-scope.md)）。
3. **スクリプトのユーザビリティ** — シナリオライターが `.rb` を書く際の使い心地。

# 方法

6 次元（基準1-3 / ADR↔実装ドリフト / ADR 判断の妥当性 / 正当性バグ）で独立レビューし、**各指摘を敵対的に再検証**（reフュート志向）した。検証で「もっともらしいが誤り」な指摘（深刻度の誇張・ADR 誤読・存在しない機構の仮定）を棄却。結果は **確認 40 件 / 棄却 7 件**。本書は深刻度を検証後の補正値で記録する。

# 総評

設計の骨格（`PlayAsync` の薄いプリミティブ・進行は game 所有・ノベル専用 Router・コア/参考 View 分離）は**基準1 の思想として的確で、実装もコア方針を守っている**。

一方で「設計が描いた到達点」と「現実装」に系統的なギャップがある。最重要は次の一点:

> **ライブラリの再利用価値の中核（タイプライタ進行エンジン・auto/skip・選択肢ロジック）が `Novel.Runtime` ではなく参考 View `NovelMessageView` に丸ごと埋まっている。** これは基準2（View 差し替えの容易さ）を直接損ない、[インラインタグ ADR](/design/decisions/inline-tags.md) が帰結で約束した「状態機械を Runtime に統合する」に反する。

# 確認済み所見

ID は後続修正の追跡キー。深刻度は検証後の補正値。

## 基準1: 埋め込みエンジンの境界

| ID | 深刻度 | 所見 | 根拠 |
|---|---|---|---|
| `NK-WORLDFX-DEAD` | medium | **`IWorldEffectSink` が完全な dead path**。配管（注入・Null 既定）だけで、handler から一度も `DispatchAsync` されない。world-effect コマンド・preamble 糖衣も無く、ゲーム本体への脱出経路が存在しない | `NovelCommandHandler.cs:22,35`（保持のみ）/ `NovelVocabulary.cs`（effect コマンド無し）/ `Preamble.rb`（糖衣無し）。[エフェクト await](/design/decisions/effect-await.md) 1 本分が宙に浮く |
| `NK-VC-VIEW-COUPLE` | low | `RegisterNovelKit` が `Novel.View`（Resources 依存）にコンパイル依存。コア分離自体は無傷だが唯一の DI 入口が参考 View を引きずる。override で回避可 | `Novel.VContainer.asmdef` / `NovelContainerExtensions.cs:17-18` |
| `NK-READ-GROWTH` | low | 既読集合に上限/クリア経路が無くセーブへ蓄積。ただし「作品の総ユニーク台詞量」で有界（無限ではない） | `MRubyStateStore.cs` `_read`（追加のみ）/ `Capture()` |
| `NK-SCENSRC-DRIFT` | low | [アーキテクチャ](/design/architecture.md):114-117 が約束した「構造化 fallback ソース + 空 id ガード」が未実装（doc ドリフト） | `ResourcesScenarioSource.cs` 1 実装のみ |

## 基準2: View 差し替えの容易さ（最重要・最大の弱点）

| ID | 深刻度 | 所見 | 根拠 |
|---|---|---|---|
| `NK-TYPEWRITER-IN-VIEW` | medium | **タイプライタ進行の状態機械が Runtime でなく View にある**。Runtime は `NovelTagLexer`（トークン化）止まり。速度蓄積・`<w>`/`<p>`/`<fast>`/`<speed>` 解釈・可視文字カーソルは View に。[インラインタグ ADR](/design/decisions/inline-tags.md) の帰結（67-68 行）が未達 | `NovelMessageView.cs:107-142` / `NovelTagLexer.cs` |
| `NK-VIEW-REIMPL-COST` | medium | 自前 View が再実装を強いられる進行ロジックが 200 行超・6 系統（トークン振り分け / Reveal ループ / 時間制御 / shake-wave 頂点 / 行末 auto-skip 待ち / `<p>` 待ち / 選択肢生成破棄） | `NovelMessageView.cs` 全体 |
| `NK-TMP-COUPLE` | medium | 進行ロジックが TMP `maxVisibleCharacters`/頂点メッシュ・uGUI Button・`Time.deltaTime` に物理的に編み込まれ、非 TMP（UI Toolkit 等）では lexer 以外ほぼ再利用ゼロ | `NovelMessageView.cs:88-91,136,154-178,203-234` |
| `NK-AUTOSKIP-IN-VIEW` | low | auto/skip 状態が View private に閉じ、`INovelView` 抽象に無い。制御 UI が具体型に密結合。FakeView では auto/skip をヘッドレス検証できていない | `NovelMessageView.cs:32-48` / `SampleControlPanel.cs` |
| `NK-FACET-SILENT` | low | 任意ファセットは Null 既定が常時登録され、game が差し替え忘れても無言で no-op。差し替え手順も未文書化 | `NovelContainerExtensions.cs:24-27` |

> 注: [インラインタグ ADR](/design/decisions/inline-tags.md) の**「決定」節**（字句解析化）は達成済み。Runtime 側状態機械への抽出は**「帰結」節**の示唆で、`NovelMessageView` は明示的に「参考実装」なので厳密な契約違反ではない（ゆえに critical でなく medium）。だが基準2 を最重要とする本ライブラリの目的に照らすと最優先の構造負債。

## 基準3: シナリオライターの書き味

| ID | 深刻度 | 所見 | 根拠 |
|---|---|---|---|
| `NK-ERROR-SILENT` | high | **エラーが既定で無音**。`INovelErrorHandler` 既定が `NullErrorHandler` で例外を握り潰し、`NovelResult.Faulted` は error 情報を持たない enum 値。runner は backtrace を surface せず（コメントと乖離）。[エラー処理 ADR](/design/decisions/error-handling.md) のビルド種別出し分けが未実装。作家が typo しても画面にもログにも何も出ない | `NovelContainerExtensions.cs:29` / `NullDefaults.cs:43-46` / `NovelResult.cs` / `NovelScenarioRunner.cs:88-93` |
| `NK-CHARNAME-SUGAR` | medium | [コマンド名規約 ADR](/design/decisions/command-schema.md) の目玉「`alice "やあ"` キャラ名糖衣」が未実装。作家は毎行 `say "alice", "..."` と話者 ID を重複タイプ | `Preamble.rb`（`say/narration/...` のみ） |
| `NK-CHOOSE-KEY` | medium | `choose` キーがグローバルカウンタ採番（順序依存で改稿耐性なし）。内部スクラッチ `__choice_N` が永続セーブに混入。[状態モデル](/design/decisions/state-model.md)/[セーブ粒度](/design/decisions/save-snapshot.md) の「永続/一時の境界を属性として扱う」が未実装。※通常フローでデータ破損はしない | `Preamble.rb:50-54` / `NovelCommandHandler.cs:57` / `MRubyStateStore.cs` |
| `NK-TMP-WHITELIST` | low | TMP タグ whitelist が `mspace`/`margin`/`font-weight` 等を取りこぼし、whitelist 外タグは無言でリテラル化。作家が誤記に気づけない | `NovelTagLexer.cs:14-20` |
| `NK-SPAN-NEST` | low | `<speed>`/`<shake>`/`<wave>` の区間が単一変数受けで入れ子・重複が静かに壊れ、未閉じ区間は捨てられる | `NovelMessageView.cs:58,74-81,125-130` |
| `NK-RESOLVE-CHOICES` | low | `ITextResolver` が `say` には適用されるが `choose` 選択肢・表示名に未適用（多言語化の seam に穴） | `NovelCommandHandler.cs:53-57` |
| `NK-VAR-READ-SUGAR` | low | 作家の変数 read が素の `state[:key]` のみ。read/get 糖衣が無く [状態モデル](/design/decisions/state-model.md) の「一貫した変数モデル」が片側だけ | `Preamble.rb` |

## 正当性バグ（ADR と独立）

| ID | 深刻度 | 所見 | 根拠 |
|---|---|---|---|
| `NK-PREAMBLE-RACE` | high | `EnsurePreambleLoadedAsync` が await 前に `_preambleLoaded=true` を立てる。初回 await が例外/ct キャンセルで失敗すると以後の全 `PlayAsync` が恒久 Faulted。※既定 `ResourcesPreambleSource` は同期完了で発火せず、カスタム async 実装で顕在化 | `NovelScenarioRunner.cs:97-103` |
| `NK-WAIT-CLICK` | low | 行内 `<w=N>` の `Delay` 中はクリックに無反応で「クリックで全文即表示」が `<w>` をまたいで効かない | `NovelMessageView.cs:115-117` |
| `NK-REENTRANCY` | low | 同一 runner への同時/再入 `PlayAsync` が単一 `_state` 共有で破綻（ガード無し）。`Restore` が additive で差分復元時に余分キー残存 | `NovelScenarioRunner.cs` / `MRubyStateStore.cs:45-49` |
| `NK-NOPARSE-INDEX` | low | 本文を `<noparse>` で包むため shake/wave の可視 index と TMP characterCount がずれる懸念（要検証） | `NovelMessageView.cs:68` |
| `NK-STABLEID` | low | `StableId` が話者+素テキストのみの 32bit FNV。分岐違いの同一文が同一既読になり、衝突も無視できない | `StableId.cs` |

# 設計判断そのものへの疑問（当時の判断を疑う）

| ID | 所見 |
|---|---|
| `NK-STATE-NS` | [状態モデル](/design/decisions/state-model.md): 単一 int 名前空間は v1 判断として妥当だが、「永続/一時/既読の境界を属性として扱う」と書きながら実装契約に落とさず、`choose` スクラッチと永続フラグが同居。ADR の自己矛盾 |
| `NK-NOOP-SILENT` | [DSL 語彙](/design/decisions/dsl-vocabulary.md): 未配線コマンドの無言 no-op は埋め込み用途でこそデバッグ地獄（誤った `.rb` と未配線 game が区別不能）。no-op 維持しつつ dev ビルドで warn すべきだった |
| `NK-REPLAY-FOUND` | [実行モデル](/design/decisions/execution-model.md): 「履歴 day1 記録は安価」を根拠に 3 下流 ADR が「将来 OK」へ寄りかかるが、その day1 記録すら未実装。記録を入れるか ADR を正直に格下げすべき |
| `NK-INLINE-SPLIT` | [インラインタグ](/design/decisions/inline-tags.md): 状態機械を Runtime に置く到達点を「帰結」節に曖昧な `TextProgressController 形` で置いたことが、View 流出の遠因 |

# 検証で棄却した指摘（誠実性のため記録）

- ❌ 「毎 `PlayAsync` で disk Load/Save は ADR 違反」→ 逆。[状態モデル](/design/decisions/state-model.md):54-55 が runner の Capture/Restore 授受を明文化済みで ADR 準拠。
- ❌ 「Router を `RegisterInstance` 登録すると複数スコープで分離保証できない」→ VContainer 子スコープ優先解決で親を決定的にシャドウ。分離は保証される。
- ❌ 「`IsReplaying`/fast-forward が凍結契約に無いのは致命的ドリフト」→ ADR が意図的に将来フェーズへ先送り。day1 必須は入力履歴記録のみ（`NK-REPLAY-FOUND` で別途）。
- ❌ 「`command-versioning` と `dsl-vocabulary` が可搬性で相殺」→ `.rb` ソース可搬性と `.mrb` 互換は別レイヤー。矛盾なし。
- ❌ 「handler を runner が new したので DI 市民性が失効」→ handler 依存は runner 経由で注入・fake 差し替え可能。失効していない。
- ❌ 「AnimateEffects と Reveal のメッシュ競合でちらつく」→ `ForceMeshUpdate` が textInfo 読取直前で世代整合。固定 PlayerLoop 順序で破綻しない。残るは軽微な余剰再生成のみ。

# 優先度付き改善ロードマップ

基準の重み（2 > 1 > 3）と費用対効果で:

**P0（基準2 の構造負債・最優先）**
1. `Novel.Runtime` に UI 非依存の進行エンジン `TextProgressController`（仮）を抽出。`NovelToken[]` + `INovelPlaybackSettings` + 進行フラグを受け `Tick(float dt)` で可視文字数/ClickWait/区間を更新。View は値を読むだけ。→ `NK-TYPEWRITER-IN-VIEW` / `NK-VIEW-REIMPL-COST` / `NK-TMP-COUPLE` を一掃し、ヘッドレステスト（`NK-AUTOSKIP-IN-VIEW`）も解決。
2. auto/skip/advance を再生セッション状態として Runtime 側へ集約。

**P1（基準3 のデバッグ体験・基準1 の脱出経路）**
3. エラー既定を無音にしない（`NK-ERROR-SILENT`）。`NovelResult.Faulted` に `NovelErrorInfo` を載せ dev 既定で `Debug.LogError`。
4. `IWorldEffectSink` を生かす（`NK-WORLDFX-DEAD`）。`WorldEffectCommand` + handler ブリッジ + preamble 糖衣。
5. キャラ名糖衣（`NK-CHARNAME-SUGAR`）。`Preamble.rb` に `method_missing`。
6. 未配線 no-op に dev 警告（`NK-NOOP-SILENT` / `NK-FACET-SILENT`）。

**P2（堅牢性・整合）**
7. preamble フラグ修正（`NK-PREAMBLE-RACE`）。`choose` 明示キー + スクラッチを Capture 除外（`NK-CHOOSE-KEY` / `NK-STATE-NS`）。`ITextResolver` を choose にも（`NK-RESOLVE-CHOICES`）。whitelist 補完（`NK-TMP-WHITELIST`）。区間スタック化（`NK-SPAN-NEST`）。`<w>` 中のクリック反応（`NK-WAIT-CLICK`）。

**P3（ドキュメント・ADR 追認）**
8. CLAUDE.md 運用規約どおり、上記ドリフトを該当 ADR の「実装で確定」節へ追認し [log](/log.md) に追記。

# 対応状況（2026-06-14）

| ロードマップ | 状態 | 対応 ID |
|---|---|---|
| P0 進行エンジン抽出 | ✅ 実装済 | `NK-TYPEWRITER-IN-VIEW` `NK-VIEW-REIMPL-COST` `NK-TMP-COUPLE` `NK-AUTOSKIP-IN-VIEW`(テスト網羅) `NK-SPAN-NEST` `NK-WAIT-CLICK` |
| P1 エラー可視化 | ✅ 実装済 | `NK-ERROR-SILENT` |
| P1 世界エフェクト | ✅ 実装済 | `NK-WORLDFX-DEAD` |
| P1 no-op 警告 | ✅ 実装済 | `NK-NOOP-SILENT` `NK-FACET-SILENT` |
| P1 キャラ名糖衣 | ✅ 実装済 | `NK-CHARNAME-SUGAR`（preamble の `chara` ヘルパ。登場キャラ分だけ `chara :alice` と書けば `alice "…"` が say 糖衣に。`method_missing` は MRubyCS 未対応のため `define_method` 方式） |
| P2 堅牢性・整合 | ✅ 実装済 | `NK-PREAMBLE-RACE` `NK-CHOOSE-KEY` `NK-STATE-NS` `NK-RESOLVE-CHOICES` `NK-TMP-WHITELIST` `NK-VAR-READ-SUGAR` |
| P3 ADR 追認 | ✅ 実施 | effect-await / inline-tags / command-schema / error-handling / state-model / dsl-vocabulary |

**追加対応済（2026-06-14, ブランチ fix/robustness-quality-enhancements）**:
`NK-REENTRANCY`（`PlayAsync` の再入/同時再生を `InvalidOperationException` で fail-fast）、
`NK-STABLEID`（既読ハッシュを 32bit → 64bit FNV-1a に強化し衝突を低減）、
`NK-SCENSRC-DRIFT`（`ResourcesScenarioSource` に空キーガード追加・`EndsWith` を Ordinal 化、architecture.md を実装（1実装+ガード）へ整合）。
`NK-NOPARSE-INDEX` は検証の結果、TMP の `<noparse>` タグは characterInfo を生成せず shake/wave の可視 index と素テキスト index が一致するため**非問題**と判断（コード変更不要）。

**設計判断・決定済**:
`NK-REPLAY-FOUND` → **「チェックポイント割り切り」で決着**（2026-06-14）。`execution-model` ADR を「リプレイ前提設計」から
「前進専用 + チェックポイント割り切り」へ格下げし、入力履歴 day1 記録・save-anywhere・ロールバックは v1 で持たないと確定。
下流（save-snapshot / determinism-contract / command-versioning / overview / architecture / open-questions）も整合済み。
現実装（境界セーブ）が既にこの形なのでコード変更は不要。

**追加対応済（2026-06-14, ブランチ refac/separate-di-helper-view-resources）**:
`NK-VC-VIEW-COUPLE` → DI ヘルパを分割。`Novel.VContainer` を **コア専用**（`RegisterNovelKitCore()`・純 `Novel.Runtime`
依存・View/Resources 非依存）にし、参考 View + Resources ローダ + 警告ファセット + ログ既定の登録は新規 asmdef
**`Novel.View.VContainer`** の `RegisterNovelKit()` へ分離（内部で Core を呼ぶ）。`Novel.VContainer.asmdef` から
`Novel.View` 参照を除去し、asmdef レベルの結合を解消。既存 `RegisterNovelKit()` の実行時挙動は不変。

**追加対応済（2026-06-15, プロジェクト独自コマンドの拡張口）**:
`NK-PROJECT-CMD` → color-recollection 導入検討で発覚した「game 固有コマンドを定義できない」隘路を解消。独自コマンド 1 つに
本来要る 3 配線（①語彙束縛 `AddCommand<T>(name)` ②ハンドラ写像 `On(T)`→Router ③Ruby 糖衣）が全て runner 内に封印されていた。
拡張口 `INovelCommandModule`（`RegisterVocabulary(MRubyState)` + `MapHandlers(ICommandSubscribable):IDisposable`）を 1 つ開け、
runner は `IEnumerable<INovelCommandModule>` を集約注入で受けて組込語彙の後に ①、組込ハンドラ写像の後に ② を実施（購読は
`Dispose` で解除）。糖衣 ③ は runner を `IEnumerable<IPreambleSource>` 化し登録順評価（組込先・game 後）で積み増す（MRubyCS は
実行時 eval 不可のため別 `.rb`→`.mrb` を追加プリアンブルで供給）。VContainer 配線は `RegisterNovelCommand<TModule>()` 1 行。
EditMode テスト 18/18 緑（`cmd :custom_echo` が game 側 `On(CustomEchoCommand)` へ到達する回帰を追加）。
詳細は [DSL 語彙の拡張節](/design/decisions/dsl-vocabulary.md)・[公開 API 表面](/design/api-surface.md)。

**追加対応済（2026-06-15, バックログ A-1 / ルビ A-2 — color-recollection 移行の前提機能）**:
- **A-1 バックログ**: `Novel.Runtime` に `IBacklog`/`BacklogEntry`/`RingBufferBacklog`（rich 保持・既定 200 行）を追加。
  `NovelCommandHandler` が say 表示ごとに話者+rich 本文を追記し、`RegisterNovelKitCore` が既定登録。逆輸入候補（§2）だった
  バックログをライブラリ標準機能化。閲覧 UI / Clear 契機は game 所有。
- **A-2 ルビ**: [インラインタグ ADR](/design/decisions/inline-tags.md) の `<ruby=よみ>漢字</ruby>` 展開を実装。lexer の
  `Ignored` 格下げを `RubyPush`(よみ)/`RubyPop` へ昇格、`TextRevealEngine.Build` がよみを可視数に算入（View 展開後の
  `characterCount` と一致させ Reveal をずらさない）、`Novel.View.RubyMarkup.BuildOverlay` が座標トリックで展開。`ToPlainText`
  はよみを除外。辞書ルビ・`:first` 初出制御は game の任意モジュール残置（分業）。
- EditMode 25/25 緑（backlog 単体3 + 統合1、ruby lexer/ToPlainText/Build 3 を追加）。

**未対応（低優先）**:
`NK-READ-GROWTH`（既読プルーン。当面は game が `CaptureState()` で得た `ReadTextIds` を保存前に間引けるため緊急度低）。

> 実装は Unity 未コンパイル状態でのコード変更。`.rb`（Preamble/サンプル）は再 import で `.mrb` 再生成が必要。
> EditMode テスト（`NovelScenarioRunnerTests` / `TextRevealEngineTests` / `NovelTagLexerTests`）で検証すること。
