---
type: Decision
title: 状態モデルは単一 IStateStore に統一
description: 永続フラグ・一時 shared-vars・既読を 1 つの IStateStore に統合し、choose() はユニークキー自動割当で衝突を防ぐ。
tags: [decision, state, flags, choice, save]
timestamp: 2026-06-14T19:35:00Z
status: 確定
---

# 状況

既存（color-recollection）では状態が 3 つに分散している。

- 永続フラグ: `ScenarioFlags`（`Dictionary<string,int>`、セーブ永続）
- 一時 shared-vars: `MRubyState.GetSharedVariables()`（choose の writeback 先）
- 既読: `SystemSaveStore` の read-flags（FNV-1a StableId）

さらに `choose()` は選択結果を `state[:last_choice]` という**単一固定スロット**に書く実装で、
プリアンブルが常にそのシンボルを読むため、ネスト/連続選択が同一スロットで衝突するバグを内包している。

# 決定

**単一 `IStateStore` に統一**する。

- 永続フラグ / 一時変数 / 既読を 1 つの `IStateStore` 抽象に統合する。
- `choose()` は予約シンボルではなく**ユニークキーを自動割当**し、ネスト/連続選択の衝突を防ぐ。
- 著者向けの変数 read / 算術 / 条件分岐もこの単一モデル上に載せる。
- リプレイの記録対象もこのストアに一元化する。

```
1 つの IStateStore: flags + vars + 既読
choose() → ユニークキー自動割当（衝突なし）
author: 変数 read / 算術 / 条件分岐もこの上
replay: 記録対象をこのストアに一元化
```

# 理由

- 3 ストア分散はエイリアス/不整合バグの温床。実際に choose() の単一スロット衝突という既存バグがある。
- リプレイ前提（[実行モデル](/design/decisions/execution-model.md)）では記録対象を一元化したい。
- 著者に一貫した変数モデル（read/算術/条件）を提供できる。

# 帰結

- choose() のユニークキー割当により、`state[:last_choice]` 固定スロットの衝突バグを解消する。
- フラグには unset/clear 経路を追加する（color-recollection の `FlagCommand` に欠けていた）。
- 永続/一時/既読の境界（どれをセーブに含めるか）は `IStateStore` 内で属性として扱う。
- 永続化は game 実装の `ISaveStore` 経由（ライブラリはシリアライズ形式を持たない）。

## 実装で確定（2026-06-14）

`IStateStore` は **runtime 内部実装（`MRubyStateStore`）を既定**とし、MRuby 共有変数テーブルを実体にする
（Ruby の `state[:key]` 読み書きと C# の flag/choose 書き込みが同一テーブルで自動同期する）。よって
「game 供給サービス」ではなく runtime が提供する。game が触れる永続化境界は `ISaveStore` のみで、runner が
`PlayAsync` の狭間で `MRubyStateStore.Capture()/Restore()` のスナップショットを授受する（[セーブ粒度](/design/decisions/save-snapshot.md)）。

# 検討した代替案

- **3 ストア維持 + choose() のみ修正**: color-recollection 構造を踏襲し単一スロット衝突だけ
  ユニークキー化。変更最小だがストア間の不整合リスクが残るため不採用。
