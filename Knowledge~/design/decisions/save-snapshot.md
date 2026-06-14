---
type: Decision
title: セーブのスナップショット粒度 — 永続は IStateStore のみ・途中保存は v1 対象外
description: 永続対象は IStateStore(フラグ/変数/既読)を ISaveStore 経由のみ。セーブ境界は PlayAsync の間。シナリオ途中保存と内容 versioning は将来。
tags: [decision, save, state, snapshot, replay]
timestamp: 2026-06-13T00:00:00Z
status: 確定
---

# 状況

[実行モデル](/design/decisions/execution-model.md) のリプレイ前提で「何を履歴に記録し何を `IStateStore`
スナップショットに含めるか・チェックポイント境界」が未定だった。[フロー/シーケンサの境界](/design/decisions/flow-boundary.md)
で「ノベルはゲームの一要素・進行とセーブは game 所有」と決めたことで大きく単純化される。

# 決定

- **永続対象は `IStateStore`（フラグ/変数/既読）のみ**。game 実装の `ISaveStore` 経由
  （[状態モデル](/design/decisions/state-model.md) / [アーキテクチャ](/design/architecture.md) で既決）。
- **セーブ境界は `PlayAsync` 呼び出しの「間」**（game が制御を持つ時点）。ノベルパートは離散セグメントで、
  game はパート間の自分のチェックポイントでセーブする。
- **シナリオ途中保存（save-anywhere）は v1 対象外**。提示状態（立ち絵/背景/再生中 SE）は**シリアライズしない**
  （途中保存しないので次回はパート先頭から）。
- [実行モデル](/design/decisions/execution-model.md) は **前進専用 + チェックポイント割り切り**（2026-06-14 にリプレイ前提から
  格下げ）。よって**入力履歴の day1 記録は持たない**。将来「長いノベルパートの途中再開」が必要になれば、その時点で
  決定性契約・履歴記録・[シナリオ内容 versioning](/design/decisions/command-versioning.md)・リプレイ実行系を**一括導入する**
  （非破壊の後付けではなく実質作り直しになることを受け入れる）。

# 理由

- 埋め込みモデルでは game がパート間でセーブするので、1 シナリオ実行の途中保存は不要。
- 提示状態は途中保存しないなら永続不要。
- 途中再開（save-anywhere）は Fiber がスナップショット不可なため将来リプレイ式で作るしかないが、それは決定性契約とセットの
  一括導入になる（[実行モデル](/design/decisions/execution-model.md)）。中間解の履歴 day1 記録は持たない。

# 帰結

- ライブラリはセーブ形式を持たず `ISaveStore` 経由（game がシリアライズを所有）。
- 途中再開・シナリオ内容 versioning は save-anywhere 実装時の課題として [残論点](/design/open-questions.md) の
  バックログに置く。
- 既読 textId のハッシュ（`StableId`、現 64bit FNV-1a）は**内部形式**で、安定版前は予告なく変わりうる。
  ハッシュ幅/算出を変えると過去セーブの既読が無効化される（既読リセット）が、**移行機構は持たない**。
  実装初期は永続セーブが存在しない＝形式確定の好機であり、形式が固まるのは安定版時点とする
  （フラグ/変数の値は影響を受けず、既読集合のみが対象）。

# 検討した代替案

- **シナリオ途中保存を v1 実装**: 埋め込みモデルでは game がパート間保存するため過剰。Fiber スナップショット不可の
  制約もあり不採用（必要時はリプレイ式で後付け）。
