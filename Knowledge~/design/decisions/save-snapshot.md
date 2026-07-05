---
type: Decision
title: セーブのスナップショット粒度 — 永続は IStateStore のみ・途中保存は v1 対象外
description: 永続対象は IStateStore(フラグ/変数/既読)を ISaveStore 経由のみ。セーブ境界は PlayAsync の間。JSON 直列化はライブラリ所有(NovelSaveSerializer)、永続先だけ game。シナリオ途中保存と内容 versioning は将来。
tags: [decision, save, state, snapshot, replay]
timestamp: 2026-07-05T00:00:00Z
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

## 実装で確定（2026-07-05, JSON serde をライブラリ所有へ）

当初の帰結「ライブラリはセーブ形式を持たず `ISaveStore` 経由（game がシリアライズを所有）」を**改め、
JSON 直列化を novel-kit 側で所有する**。`Novel.Runtime` に純 C#（UnityEngine 非依存・headless テスト可）の
`NovelSaveSerializer`（`NovelStateSnapshot` ⇔ JSON 文字列）を追加した。粒度・境界の決定（永続は `IStateStore` の
`Capture()` 結果のみ・境界は `PlayAsync` の狭間）は不変で、**変わったのは「形式の所有者」だけ**。

- 形式（決定的。キー/既読 id を序数ソートし diff/テストを安定化）:
  `{"version":1,"values":{"coins":30,"met_taylor":1},"read":["a1b2c3d4e5f60718"]}`。
  `version` は将来のスキーマ移行フック（現状は読むだけ）。既読 id は `StableId`（64bit FNV-1a の 16 桁 hex）。
- **IO は持たない**。「JSON をどこへ書くか」は `INovelSaveBlobStore`（`Write/ReadAsync(string)`）として game に委譲。
  既定実装 `JsonSaveStore : ISaveStore` が serde と blob store を束ね、破損/未作成セーブは空 snapshot へ
  フォールバック（`LoadAsync` は throw しない＝新規開始）。
- Unity 実装例 `PlayerPrefsSaveBlobStore`（`Novel.View`）を同梱。DI は `RegisterNovelJsonSave<TBlob>()`
  1 行でコアの `NullSaveStore` を後勝ち上書き。
- **理由**: color-recollection ではフラグ/既読の直列化が game ごとに再実装され不統一だった。novel-kit は状態を単一
  `IStateStore` に統合済み（[状態モデル](/design/decisions/state-model.md)）なので、その snapshot の JSON 形式を
  ライブラリが 1 つ持てば全 game で一貫する。純 C# に留めることで Runtime の headless テスト可能性も保つ。
- 既読 id ハッシュが**内部形式**である点（安定版前は予告なく変わりうる・移行機構なし）は従来どおり。`version` 併記で
  将来の一括移行の足場だけ用意した。EditMode テスト `NovelSaveSerializerTests`（往復・決定性・エスケープ・破損耐性）。

# 検討した代替案

- **シナリオ途中保存を v1 実装**: 埋め込みモデルでは game がパート間保存するため過剰。Fiber スナップショット不可の
  制約もあり不採用（必要時はリプレイ式で後付け）。
