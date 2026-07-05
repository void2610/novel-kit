---
type: Decision
title: セーブのスナップショット粒度 — 永続は IStateStore のみ・途中保存は v1 対象外
description: 永続対象は IStateStore(フラグ/変数/既読)のみ。snapshot 授受は runner の CaptureState/RestoreState、直列化は NovelSaveData/NovelSaveSerializer、保存は game 所有(ISaveStore は撤去)。セーブ境界は PlayAsync の間。シナリオ途中保存と内容 versioning は将来。
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
JSON 直列化(serde)を novel-kit 側で持つ**。ただし**永続化（ファイルへ書く行為）は引き続き game 所有**で、
novel-kit が渡すのは「セーブデータの表現（JSON 文字列 or シリアライズ可能クラス）」まで。粒度・境界の決定
（永続は `IStateStore` の `Capture()` 結果のみ・境界は `PlayAsync` の狭間）は不変。

**スナップショットの授受は runner が直接露出する（`ISaveStore` を撤去）**。当初は「runner が `PlayAsync` の
狭間で `ISaveStore.Save/LoadAsync` を自動で呼ぶ」設計だったが、これは (1) 再生フローに game の IO(await)を
差し込む、(2)「毎シナリオ境界で保存」というポリシーを runner が押し付ける、(3) game が既に「いつ保存するか」を
所有している（[フロー境界](/design/decisions/flow-boundary.md)）のにコールバックで制御が逆転する、という問題が
あった。よって **`ISaveStore` / `NullSaveStore` を廃止**し、runner に snapshot の出し入れだけを露出する:

```
NovelStateSnapshot INovelScenarioRunner.CaptureState();      // 保存したい時に game が引く
void INovelScenarioRunner.RestoreState(NovelStateSnapshot);  // continue 時、次の PlayAsync より前に戻す
```

live state はセッション中 runner が保持する。game は continue で一度 `RestoreState`、保存したいタイミングで
`CaptureState()` → 直列化 → 自前 save、という流れになり、**再生フローから IO が消える**。

**想定する主用途（標準）**: game は自前の JSON セーブ機構を持つ。novel-kit は snapshot を
**文字列 or クラスとして書き出す**だけで、保存は game に任せる。

- **クラス**: `NovelSaveData`（`Novel.Runtime`・公開・`[Serializable]` プレーンフィールド）。`From(snapshot)` /
  `ToSnapshot()`。JsonUtility/Newtonsoft/System.Text.Json いずれの自前 serde でも**そのままネストできる**
  形（Dictionary を避けキー/値ペアのリスト）。
  例: `mySave.novel = NovelSaveData.From(runner.CaptureState());`
- **文字列**: `NovelSaveSerializer.Serialize(snapshot)` / `TryDeserialize`。内部で `NovelSaveData` + Unity 標準
  `UnityEngine.JsonUtility`（`com.unity.modules.jsonserialize`・追加パッケージ不要）を使う。
  出力（決定的。キー/既読 id を序数ソート）:
  `{"version":1,"values":[{"key":"coins","value":30}],"read":["a1b2c3d4e5f60718"]}`。

**永続化はライブラリに持ち込まない**: 「novel-kit に保存まで丸ごと任せる」モードは提供しない（ファイル/
PlayerPrefs 等への書き込みは game のセーブ機構の責務であり、ライブラリが抱えるべきでない）。novel-kit の
責務はセーブデータの**表現（`NovelSaveData` / JSON 文字列）まで**で終わる。

- 破損/未作成セーブは game 実装側で空 snapshot にフォールバックする（`NovelSaveSerializer.TryDeserialize` が
  false + `Empty` を返すのを利用できる）。
- `version` は将来のスキーマ移行フック（現状は読むだけ）。既読 id は `StableId`（64bit FNV-1a の 16 桁 hex）。
- **理由**: color-recollection ではフラグ/既読の直列化が game ごとに再実装され不統一だった。novel-kit は状態を単一
  `IStateStore` に統合済み（[状態モデル](/design/decisions/state-model.md)）なので、その snapshot の JSON 表現を
  ライブラリが 1 つ持てば全 game で一貫する。実際の保存は game の既存セーブ機構に委ね、novel-kit はデータの
  表現だけを担う。Unity 標準の JsonUtility を使い自前パーサや外部依存を避ける。
- 既読 id ハッシュが**内部形式**である点（安定版前は予告なく変わりうる・移行機構なし）は従来どおり。`version` 併記で
  将来の一括移行の足場だけ用意した。EditMode テスト `NovelSaveSerializerTests`（往復・決定性・エスケープ・破損耐性）。

# 検討した代替案

- **シナリオ途中保存を v1 実装**: 埋め込みモデルでは game がパート間保存するため過剰。Fiber スナップショット不可の
  制約もあり不採用（必要時はリプレイ式で後付け）。
