---
type: Decision
title: フロー/シーケンサの境界 — 進行は完全 game 所有
description: novel-kit はゲーム内の一要素としてのノベルパート。シーケンサや goto は持たず、進行は game 側が PlayAsync で駆動する。
tags: [decision, flow, sequencer, scope, runner]
timestamp: 2026-06-13T00:00:00Z
status: 確定
---

# 状況

ライブラリの表面積（`PlayAsync` のみ vs 汎用シーケンサ所有）の線引きが未確定だった。批判レビューは
「薄すぎると全 game が color-recollection の `NovelPresenter` 相当の orchestration を再実装する」と懸念していた。

# 決定

## novel-kit は「ゲーム内の一要素としてのノベルパート」エンジン

- 想定はスタンドアロン VN ではなく、より大きなゲームの中の**ノベルパート**。
- したがって進行（どのシナリオをいつ出すか・章/フェーズ・分岐・auto-advance・retry・resume）は
  **完全に game 側の責務**とする。game がノベル側に「このシナリオを再生せよ」と命令を出す。
- ライブラリは「命令されたら 1 シナリオを再生する」プリミティブに徹する。

## 具体

- コア表面は `UniTask<NovelResult> PlayAsync(scenarioKey, ct)`（1 シナリオを完了まで再生し結果を返す）。
- **線形シーケンサ／汎用シーケンサは同梱しない**（`Novel.Flow` のようなモジュールを持たない）。
- **`.rb` に `goto`（次シナリオ指定）を書かない**。シナリオ遷移は game 側ロジックがノベルに命令を出す形。
- `NovelResult` は完了状態（completed / cancelled）に留める。分岐に必要な outcome（どの選択をしたか等）は
  [単一 IStateStore](/design/decisions/state-model.md) のフラグ/変数として残るので、game は `PlayAsync` 完了後に
  状態ストアを読んで次を決める。

# 理由

- ノベルがゲームの一要素なら、進行は game 自身の状態機械の一部であり、ライブラリが所有すると二重管理・
  押し付けになる。
- 「薄すぎ」懸念は本フレーミングでは当たらない。重い orchestration がそもそも存在せず、game が自分のフロー中で
  `PlayAsync` を呼ぶだけだから。
- シナリオに `goto` を持たせない＝シナリオは 1 本完結の純粋な提示単位になり、テスト/再利用/リプレイがシンプルになる。

# 帰結

- ランナー API は `PlayAsync(key, ct) -> NovelResult` に確定。
- フロー/章/シナリオ選択ロジックは game 側。color-recollection の phase/`KeywordGate`/resume bootstrap 等は
  ライブラリに取り込まない。
- [アーキテクチャ](/design/architecture.md) の「フロー制御は game 側」を本決定が確定・強化する。
- [概要](/design/overview.md) のスコープに「ゲーム内ノベルパート（スタンドアロン VN ではない）」を明記する。
- save-anywhere の優先度は相対的に下がる（埋め込みの短いノベルパートはゲーム全体のセーブ機構に従うことが多い）。
  [実行モデル](/design/decisions/execution-model.md) は前進専用 + チェックポイント割り切り（履歴記録/save-anywhere は持たない）。

# 検討した代替案

- **任意の薄い線形シーケンサを同梱**: 共通 await ループの再実装を省けるが、ノベルパート想定では game が進行を
  持つため不要。不採用。
- **シナリオ側 `goto` / next ヒント**: 著者が `.rb` に分岐フローを書けるが、進行 game 所有の方針と二重化する。不採用。
