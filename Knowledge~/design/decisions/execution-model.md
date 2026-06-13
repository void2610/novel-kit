---
type: Decision
title: 実行モデルはリプレイ前提設計
description: 入力ログ + .rb ヘッドレス再実行で進行を再現し、save-anywhere/ロールバックを非破壊で後付け可能にする。
tags: [decision, runtime, save, replay, rollback]
timestamp: 2026-06-13T00:00:00Z
status: 確定
---

# 状況

全 MRuby プロジェクトは「`async On(cmd,ct)` を VitalRouter が await して Ruby Fiber を
サスペンドする」前進専用の進行モデルを採用している（[アーキテクチャ](/design/architecture.md) 参照）。
しかしサスペンド中の Fiber はスナップショットできないため、この方式のままでは
**save-anywhere（任意行でのセーブ/再開）と Ren'Py 式ロールバックが構造的に作れない**。
既存最良の color-recollection でも `PlaySaveData` は phase + flags + keywords のみで行カーソルを
持たず、再入場時は `.rb` を先頭から再生していた（[機能棚卸し](/design/feature-inventory.md) の「欠落」参照）。

# 決定

**リプレイ前提設計** を採る。

- 選択 index・フラグ操作などの**入力履歴を day 1 から記録**する（記録自体は低コスト）。
- 当面の復帰はチェックポイント（phase/シーン単位）。
- 将来の save-anywhere / ロールバックは、保存地点まで**同じ `.rb` を UI スキップ + 履歴注入で
  ヘッドレス高速再実行**し、そこから通常進行する方式で非破壊に追加する。

```
runner.PlayAsync(key, history?)
  history = [choice#0=1, flag set, choice#1=0, ...]
再開: 同じ .rb を UI スキップ + 履歴注入で保存地点まで高速再実行 → そこから通常進行
```

# 理由

- Fiber サスペンション進行モデルの利点（手書き状態機械が不要・実証済み）を維持できる。
- 履歴記録は安価で、最初から仕込めば後で実行モデルを作り直さずに済む（中間解）。
- Naninovel/Ren'Py 相当の体験への発展余地を、初期コストを抑えつつ残せる。

# 帰結

- ランナーは再実行時の高速・無表示モードを示す `IsReplaying`/fast-forward コンテキストを
  ハンドラ・View に通す設計にする必要がある。
- 再実行で同じ地点に着くには**シナリオの決定性**が前提になる。ただしその強制（seed 付き RNG・
  `Time` 直接使用の禁止）は当面見送る → [決定性コントラクト](/design/decisions/determinism-contract.md)。
- 記録対象は単一の状態ストアに一元化する → [状態モデル](/design/decisions/state-model.md)。

# 検討した代替案

- **完全 save-anywhere + ロールバックを v1 実装**: 決定的リプレイエンジンを最初から構築。Naninovel/
  Ren'Py 相当だがシナリオ決定性の強制と実装コストが大きい。→ 見送り（将来の発展先）。
- **チェックポイント割り切り**: 既存 3 プロジェクト踏襲、save-anywhere/ロールバック非対応と明記。
  最小だが後付けが実質作り直しになるため不採用。
