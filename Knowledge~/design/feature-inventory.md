---
type: Reference
title: 既存実装の機能棚卸しと統合機能案
description: 既存 6 Unity プロジェクトのノベル機能マトリクスと、novel-kit へ統合する機能の優先度別リスト。
tags: [survey, feature-matrix, inventory]
timestamp: 2026-06-13T00:00:00Z
---

# 調査方法

`/Users/shuya/Documents/GitHub` 配下の 6 Unity プロジェクトを並列エージェントで深掘り読解し、
機能・MRuby コマンド語彙・DI/ルーティング配線を構造化抽出 → 統合 → VN エンジン観点で批判レビューした。

# プロジェクト別の位置づけ

| プロジェクト | 成熟度 | 貢献 |
|---|---|---|
| color-recollection | mature | リファレンス基盤。choose 分岐・永続フラグ・既読/バックログ/スキップ・キーワード/ルビ markup・構造化カタログ。`INovelView` シーム、純 C# テキストパイプライン、`INovelPlaybackSettings` が最良の抽出対象 |
| unity1week-2026-03 | mixed | 唯一の公式 `RegisterVitalRouter` 統合。最広のコマンド語彙（speak/subtitle/portrait/bg/still/effect/se/wait/blackout）+ 堅牢な ID 式 VoiceService + 字幕スタイル |
| apocalyptic-apartment-hunting | partial | 2 ルーター構成（private dialogue Router + `Router.Default` への Effect ブリッジ）、`IDialogueAdvanceSource`/`IDialogueGate`、ランタイム切替 View |
| void-red / garden-gnome / otajam | mixed | 提示層アイデアバンク（DSL 対象外）。void-red の `TextProgressController`（純 C# タイプライタ状態機械）、2 画像クロスフェード立ち絵、await 式パッド対応選択 UI、`Void2610.UnityTemplate` |

# 機能マトリクス（best-of-breed）

各機能の最良実装（借用元）。CR=color-recollection, U1W=unity1week-2026-03, AAH=apocalyptic-apartment-hunting。

| 機能 | 最良実装 | 備考 |
|---|---|---|
| タイプライタ文字送り | CR（deltaTime アキュムレータ）+ void-red `TextProgressController`（純 C# エンジン形） | 行内タグ対応のため字句解析式へ再設計予定 |
| ルビ（ふりがな） | CR（唯一） | JP テキストモジュールとして任意化 |
| インライン clickable markup | CR（`[[id|word]]` link + hit-test） | キーワード収集ゲーム要素と分離して汎用化 |
| 行スタイル/強調 | U1W（Default/Weak/Impact/Whisper + speaker tint） | 行単位スタイル属性 |
| 話者名表示 | AAH（CharacterCatalog で id→表示名/立ち絵/side）+ CR（空=ナレ） | |
| メッセージ窓 表示/フェード | void-red `BaseWindowView` + AAH backdrop dim | |
| 複数提示スタイル（窓/吹き出し/ナレ） | AAH（`DialogueStyleController` 行ごと切替）+ void-red 変種 | |
| クリック送り/入力待ち | AAH（`IDialogueAdvanceSource` + 二重消費ガード）+ CR（段階 Advance） | |
| Auto | CR（設定連動遅延）+ void-red（SE 長を待つ） | |
| Skip | CR（既読考慮の連続スキップ・skip-includes-unread） | 唯一の本格実装 |
| バックログ | CR（200 行 + クリック復帰） | 純 C# バッファ |
| 選択肢/分岐 | CR（MRuby 分岐配線）+ void-red（await 式パッド対応 UI） | |
| コンテキスト駆動セリフ選択 | U1W（Trigger+state→Category→fallback→random） | systemic barks 用・任意 |
| フラグ/変数 | CR（永続 `Dictionary<string,int>`） | unset 追加が必要 |
| セーブ/レジューム | CR（phase チェックポイントのみ・行カーソル無し） | ※下記「欠落」参照 |
| 音声（ID 式） | U1W（id→cue→variant, anti-repeat, wildcard, cooldown, fade） | |
| BGM/SE | U1W（`play_se`）+ void-red（名前付き SE 表・clip 長返却） | |
| 立ち絵 + 表情 | void-red（2 画像クロスフェード）+ U1W（`set_portrait` 語彙） | 単一スプライト方針 |
| 背景/CG | U1W（`set_background`/`set_still`）+ void-red（fade-through-black） | |
| カメラ振動/snap・演出 | AAH（Effect ブリッジ）+ U1W（CinematicSequenceAsset） | 任意 |
| トランジション/フェード | void-red（LitMotion 拡張一式） | |
| MRuby コマンドバス | U1W（DI 配線）/ AAH（2 ルーター）/ CR（語彙の広さ） | 所有権 3 者三様 → 統一必要 |
| 非同期 await で Fiber サスペンド | 全 3（同一・実証済み） | 進行モデルの基盤 |
| シナリオソース/アドレッシング | CR（構造化カタログ + fallback + irep cache + editor fallback） | |
| `.mrb` バイトコードロード | CR（ParseBytecode + editor compile fallback） | native libmruby 不要 |
| プリアンブル DSL 糖衣 | CR + U1W（一度ロード） | |
| 設定/再生コンフィグ | CR（`INovelPlaybackSettings` + Default fallback） | |

# 統合機能案（優先度別）

## CORE（土台）
MRuby シナリオランナー + per-scope VitalRouter コマンドバス / Fiber サスペンション進行 /
`.mrb` バイトコードロード + `IScenarioSource` / `INovelView` / 正規 say/narration + `NovelLine` /
プリアンブル DSL / `ParseSymbol<T>` / タイプライタエンジン / 注入可能 advance 入力源 + プレイヤーゲート。

## STANDARD
選択肢/分岐 / 永続フラグ（+unset）/ 既読追跡 + 既読色 + 既読考慮スキップ / Auto/Skip（設定連動・SE 長待ち）/
バックログ / `INovelPlaybackSettings` / 立ち絵コマンド + クロスフェード View / 背景/CG（fade-through-black）/
音声コマンド（se/bgm・名前付き SE 表）/ LitMotion アニメ + `BaseWindowView`。

## OPTIONAL
ID 式 VoiceService（voice_id）/ Effect ブリッジ / Cinematic effect / ランタイム View 切替 /
ルビふりがな / インライン clickable markup / 行スタイル / wait / フェーズ連動フック /
コンテキスト駆動セリフ選択 / item-get 等の interstitial。

# ジャンル標準なのに全プロジェクトに欠落（VN エンジン観点の批判レビュー）

汎用 VN ライブラリには必要だが、調査した全プロジェクトに無い。Fiber サスペンション進行モデルと
衝突するものはランナー API 凍結前に方針を決める必要がある。

| 欠落 | 検証された事実 | 難点 |
|---|---|---|
| セーブ・エニウェア/レジューム | CR の `PlaySaveData` は phase+flags+keywords のみ。**行カーソル無し**、再入場で `.rb` を先頭から再生 | サスペンド中の Fiber はスナップショット不可。リプレイ方式が必要 → [実行モデル](/design/decisions/execution-model.md) |
| ロールバック（巻き戻し + 選択やり直し） | 全 3 で不在。バックログは表示専用 | 前進専用 Fiber と根本的に非互換 |
| 行内インラインタグ | 行単位変換のみ。`say()` 内の途中ポーズ/速度/部分着色が不可 | エンジン再設計が必要 → [インラインタグ](/design/decisions/inline-tags.md) |
| MRuby ランタイムエラー処理/サンドボックス | `ExecuteAsync` 周りに try/catch ゼロ。nil 呼び 1 つで未処理例外、行番号も出ない | → [残論点](/design/open-questions.md) |
| ローカライズ | テキストが `.rb` 内 Ruby 文字列リテラルにハードコード | → [ローカライズ](/design/decisions/localization.md) |
| キャラ多層合成/ステージ配置 | `set_portrait` は単一スプライト差し替えのみ | → [キャラクターモデル](/design/decisions/character-model.md) |

# 過大評価だった点

- CR の「セーブ/レジューム」は phase チェックポイントであり VN の途中再開ではない。
- U1W の voice は「キュー解決」は堅牢だが voice-drives-auto-advance 等は無く「VN 音声解決」止まり。
- choose() の共有状態 writeback は `state[:last_choice]` 単一スロットに固定で、ネスト/連続選択で衝突する
  バグを内包（ADR [状態モデル](/design/decisions/state-model.md) でユニークキー化）。
