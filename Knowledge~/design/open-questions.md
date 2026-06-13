---
type: Design
title: 残論点（未決の設計判断）
description: ランナー API 凍結前に解消すべき未決事項と、後続で詰める設計トピック。
tags: [open-questions, todo, design]
timestamp: 2026-06-13T14:40:00Z
status: 保留
---

# 凍結前に決めるべき（高優先）

> 2026-06-13: 本セクションは**全て解決済み**。残るは下記「中優先」と「機能バックログ」。

## コマンドスキーマのバージョニング/エイリアス戦略 ✅ 解決済み
→ [コマンドスキーマ versioning](/design/decisions/command-versioning.md)（versioning 機構は持たない・`.rb` 正で
再生成追従・Ruby 名エイリアスは任意利便機能）で確定。シナリオ内容 versioning（save/replay 互換）は別概念として
下記「セーブのスナップショット粒度」に統合。

## コマンド名規約 ✅ 解決済み
→ [コマンド名規約と say スキーマ](/design/decisions/command-schema.md)（`say` 一本 + 糖衣、話者は id 基本のハイブリッド、`[Routes]` 規約）で確定。

## インラインタグ構文の正規定義 ✅ 解決済み
→ [行内インラインタグ](/design/decisions/inline-tags.md)（TMP `<...>` 単一・ライブラリ所有 lexer・非タグはエスケープ・
パースは Novel.Runtime・キーワードは TMP `<link>`・リテラルは `<noparse>`）で確定。v1 タグセットの細部は実装時に最終確定。

## ルーター所有権 ✅ 解決済み
→ [ルーター所有権](/design/decisions/router-ownership.md)（ノベル専用 Router を container 登録・ハンドラ DI 市民・
世界エフェクトは game 供給先への明示ブリッジ既定なし・provider 抽象は入れない）で確定。

# 中優先（コア周辺）

> 2026-06-13: 本セクションも**全て解決済み**。未決は下記「機能バックログ」（v1 スコープ外）のみ。

## エフェクトブリッジの await 意味論 ✅ 解決済み
→ [エフェクトの await 意味論](/design/decisions/effect-await.md)（エフェクトは進行モデルと同じ「ハンドラを await」で
blocking/non-blocking を表現・`IWorldEffectSink` は async・per-call 上書きは v1 無し）で確定。

## フロー/シーケンサの境界 ✅ 解決済み
→ [フロー/シーケンサの境界](/design/decisions/flow-boundary.md)（ゲーム内ノベルパート前提・進行は完全 game 所有・
シーケンサ/シナリオ間 `goto` は持たない・コア表面は `PlayAsync(key,ct) -> NovelResult` のみ）で確定。

## セーブのスナップショット粒度（リプレイとの整合）✅ 解決済み
→ [セーブのスナップショット粒度](/design/decisions/save-snapshot.md)（永続は `IStateStore` のみ・`ISaveStore` 経由・
セーブ境界は `PlayAsync` の間・シナリオ途中保存は v1 対象外・提示状態は非シリアライズ）で確定。
途中再開（リプレイ式 save-anywhere）とシナリオ内容 versioning は下記バックログへ。

## 音声のスコープ ✅ 解決済み
→ [音声スコープ](/design/decisions/audio-scope.md)（voice は v1 除外で将来は別コマンド+糖衣、SE/BGM は v1 採用、
lipsync は対象外）で確定。残るのは `se`/`bgm` コマンドの引数詳細（音量/フェード/ループ/pitch/停止）で実装時に詰める。

## MRuby ランタイムエラー処理/サンドボックス ✅ 解決済み
→ [MRuby エラー処理・サンドボックス](/design/decisions/error-handling.md)（try/catch で backtrace surface・
`NovelResult.Faulted` でフェイルセーフ・`INovelErrorHandler` 注入・サンドボックスは v1 無し＝一次コンテンツ前提）で確定。

# 機能バックログ（v1 スコープ外だが将来検討）

- ロールバック（Ren'Py 式巻き戻し）。[実行モデル](/design/decisions/execution-model.md) のリプレイ基盤の上で将来。
- 長いノベルパートの途中再開（リプレイ式 save-anywhere）+ シナリオ内容 versioning。[セーブのスナップショット粒度](/design/decisions/save-snapshot.md) で v1 対象外。
- メッセージ窓の hide トグル（シーン中に窓を一時的に隠し背景/立ち絵を見る、ノベルパート内の提示アフォーダンス）。
  - 注: CG 鑑賞ギャラリー（解放済み CG の一覧/コレクション）は**スコープ外**。ゲーム全体のメタ機能であり、本ライブラリはゲーム内ノベルパートのプリミティブに徹する（[フロー境界](/design/decisions/flow-boundary.md)）。
- 選択肢のアフォーダンス: 無効/条件付き（grey out）・タイマー付き・一度のみ。
- アクセシビリティ: 文字サイズ・可読フォント・reduce-motion・hold-to-skip。
- 著者向けプレビュー/シーンジャンプ/分岐バリデータ（`Novel.Editor`）。
- `.rb` のホットリロード（編集 → 再 import なしの反復ループ）。
- `Novel.View` の外部依存スタンス（`Void2610.UnityTemplate`/LitMotion を依存とするか vendoring するか）。
- コンテキスト駆動セリフ選択（systemic barks、U1W 由来）を任意モジュール化。
- UGC（ユーザー生成）シナリオを許す場合の MRuby サンドボックス/capability 制限（[エラー処理](/design/decisions/error-handling.md) で v1 は一次コンテンツ前提）。
