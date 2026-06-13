---
type: Decision
title: エフェクトの await 意味論 — ハンドラ await で統一・ブリッジは async
description: エフェクトは進行モデルと同じ「ハンドラを await」で blocking/non-blocking を表現。IWorldEffectSink は async。per-call の wait 上書きは v1 無し。
tags: [decision, effect, bridge, await, async, runner]
timestamp: 2026-06-13T00:00:00Z
status: 確定
---

# 状況

[ルーター所有権](/design/decisions/router-ownership.md) で世界エフェクトは「game 供給先への明示ブリッジ・既定なし」と
決めたが、await 意味論が未設計だった。批判レビュー: apocalyptic のブリッジは `Router.Default` へ撃ちっぱなしで、
「次行前に終わる必要のある 2 秒ブラックアウト」のような完了待ちエフェクトの ordering が表現できない。

# 決定

## エフェクトは進行モデルと同じ「ハンドラを await」で統一

進行は既に「`async On(cmd,ct)` を await・**非ブロッキング = 即 return / ブロッキング = 完了まで await**」
（say と同じ）。エフェクトも同モデルに乗せ、**ハンドラが await するか否かで** blocking/non-blocking が決まる。

- **非ブロッキング**: エフェクトを開始して即 return（内部で Forget）。例: `shake` / 一瞬の flash（会話は止まらない）。
- **ブロッキング**: 完了まで await（Fiber サスペンド = 次行が待つ）。例: `fade_out(2.0)` / blackout / トランジション。

DSL に「ブロッキング」フラグは入れない。ハンドラ挙動で決まる（[実行モデル](/design/decisions/execution-model.md) の
Fiber サスペンションと同一機構）。

## 世界エフェクトのブリッジを async にする

- `IWorldEffectSink` を **async（`UniTask` 返し）** にする。ブリッジは fire-and-forget でなく await する。
- 非ブロッキング世界エフェクト（shake）→ game の sink が**即完了タスク**を返す → 待たない。
- ブロッキング世界エフェクト（2 秒ブラックアウト cinematic）→ sink が**完了時に解決するタスク**を返す →
  ランナーが await → Fiber サスペンド → 次行が待つ。

## per-call 上書きは v1 無し

- 内蔵エフェクトの blocking 性はコマンドごとに固定（`fade_out`=blocking、`shake`=non-blocking 等）。
- per-call の `wait:` 上書き（`shake wait: true` 等）は v1 では持たない。必要が出たら後で追加。

# 理由

- 進行モデルと同一機構なので一貫し、追加概念がゼロ。
- ブリッジを async にするだけで「撃ちっぱなし固定」が解消し、完了待ちエフェクトが正しく表現できる。
- per-call 上書きは表面積を増やすだけで当面不要。

# 帰結

- [ルーター所有権](/design/decisions/router-ownership.md) で「await 意味論は別途」とした残件を本決定が確定。
- `IWorldEffectSink` は `UniTask` を返すインターフェース。
- 内蔵エフェクトコマンド（`fade_out`/`blackout`/`shake`/`flash` 等）の blocking 性は実装時に各コマンドで定義する。

# 検討した代替案

- **全エフェクト fire-and-forget + 明示 `wait` コマンドで同期**: 著者が手動同期。ハンドラ await 統一の方が自然で不採用。
- **per-call `wait:` 上書きを v1 導入**: 表面積増。当面不要で見送り（将来）。
