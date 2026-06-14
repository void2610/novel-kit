---
type: Decision
title: 実行モデルは前進専用 + チェックポイント割り切り
description: Fiber サスペンション進行を維持し、セーブ/復元は PlayAsync 境界のみ。入力履歴の day1 記録・save-anywhere・ロールバックは v1 で持たない。
tags: [decision, runtime, save, checkpoint, replay]
timestamp: 2026-06-14T23:50:00Z
status: 確定（2026-06-14 にリプレイ前提から格下げ）
---

# 状況

全 MRuby プロジェクトは「`async On(cmd,ct)` を VitalRouter が await して Ruby Fiber を
サスペンドする」前進専用の進行モデルを採用している（[アーキテクチャ](/design/architecture.md) 参照）。
サスペンド中の Fiber はスナップショットできないため、この方式のままでは
**save-anywhere（任意行でのセーブ/再開）と Ren'Py 式ロールバックが構造的に作れない**。
既存最良の color-recollection でも `PlaySaveData` は phase + flags + keywords のみで行カーソルを
持たず、再入場時は `.rb` を先頭から再生していた（[機能棚卸し](/design/feature-inventory.md) の「欠落」参照）。

# 決定

**前進専用 + チェックポイント割り切り** を採る。

- Fiber サスペンション進行モデルをそのまま使う（手書き状態機械が不要・実証済み）。
- セーブ/復元は **`PlayAsync` 呼び出しの「間」のみ**（チェックポイント = シナリオ/パート境界）。実体は runner が
  `PlayAsync` 冒頭で `Restore`・末尾で `Save`（[セーブのスナップショット粒度](/design/decisions/save-snapshot.md)）。
- **入力履歴（選択 index・フラグ列）の記録は持たない**。`IsReplaying`/fast-forward 配線も持たない。
  save-anywhere / ロールバックは v1 非対応。
- 将来 save-anywhere / ロールバックが本当に必要になったら、その時点で
  「決定性契約 + 入力履歴記録 + シナリオ内容 versioning + リプレイ実行系」を**一括導入する**
  （＝実質的な作り直しになることを受け入れる。中間解は採らない）。

```
runner.PlayAsync(key, ct) -> NovelResult
セーブ/復元: PlayAsync の狭間（チェックポイント）でのみ
途中保存・巻き戻し・履歴記録は持たない
```

# 経緯（なぜリプレイ前提から格下げしたか）

当初（2026-06-13）は「入力履歴を day1 から記録すれば、後で save-anywhere を**非破壊に後付けできる安価な土台**になる」
としてリプレイ前提設計を採った。だが実装フェーズの検証で次が判明し、2026-06-14 に本決定へ格下げした。

1. **その day1 記録すら実装されなかった**（`IsReplaying`/fast-forward 配線も含め一式未着手）。土台が空洞のまま
   下流 ADR（[セーブ粒度](/design/decisions/save-snapshot.md) / [決定性コントラクト](/design/decisions/determinism-contract.md) /
   [コマンドスキーマ versioning](/design/decisions/command-versioning.md)）が「将来 OK」に寄りかかっていた。
2. **「記録は安価」だが「再現は安価でない」**。決定的リプレイは `rand`/`Time`/外部状態の決定性強制を伴い、それを
   [決定性コントラクト](/design/decisions/determinism-contract.md) で後回しにした時点で「記録した履歴で再現できる」保証は
   崩れる（同 ADR 自身がこのリスクを明記）。つまり day1 記録は決定性契約とセットでしか機能せず、単独では土台にならない。
3. 埋め込みノベルパートは短く、ゲーム全体のセーブ機構に従うことが多い（[フロー境界](/design/decisions/flow-boundary.md)
   でも save-anywhere の優先度は下がると既述）。境界セーブで実用上十分。

よって「未実装の中間解を抱えて負債だけ残す」より、現実装（境界セーブ）に正直に合わせる。

# 理由

- Fiber サスペンション進行モデルの利点（手書き状態機械が不要・実証済み）を維持できる。
- 埋め込み用途では game がパート間でセーブするため、境界チェックポイントで実用上足りる。
- 中間解（day1 記録）が決定性契約なしに機能しない以上、save-anywhere は「やるなら一括」が正直。

# 帰結

- ランナー表面は `PlayAsync(key, ct) -> NovelResult` のまま。`history` 引数や `IsReplaying`/fast-forward 配線は持たない。
- [決定性コントラクト](/design/decisions/determinism-contract.md) は本決定下では **moot**（リプレイが無いので決定性強制の
  動機が消える）。save-anywhere を将来やる段で改めて規定する。
- save-anywhere / ロールバック / 入力履歴記録 / シナリオ内容 versioning は [残論点](/design/open-questions.md) の
  バックログに「将来やるなら一括・実質作り直し」として置く。

# 検討した代替案

- **リプレイ前提設計（当初の決定・2026-06-13）**: 入力履歴 day1 記録で save-anywhere を非破壊後付けする中間解。
  記録が決定性契約なしに機能せず、かつ未実装のまま負債化したため格下げ。
- **完全 save-anywhere + ロールバックを v1 実装**: 決定的リプレイエンジンを最初から構築。Naninovel/Ren'Py 相当だが
  シナリオ決定性の強制と実装コストが大きい。→ 見送り（将来の発展先）。
