---
type: Decision
title: コマンドスキーマ versioning は持たない（.rb 正・再生成で追従）
description: .rb を唯一のソース、.mrb は再生成成果物。重厚な versioning 機構は v1 で持たない。save/replay 互換は別概念として分離。
tags: [decision, versioning, mrb, bytecode, compatibility, save]
timestamp: 2026-06-13T00:00:00Z
status: 確定
---

# 状況

[DSL 語彙: リッチ統一語彙を常設](/design/decisions/dsl-vocabulary.md) を選んだため `.mrb` に焼かれる互換境界が
広いという懸念があった。ただし互換境界の所在は **Ruby 表層の名前**（コマンド名 / preamble メソッド名 / kwarg 名）
であり、C# プロパティ名は MRubyCS.Serializer の名前マッピングで吸収できる
（[コマンド名規約と say スキーマ](/design/decisions/command-schema.md) の「互換境界の所在」）。

# 決定

## コマンドスキーマ versioning 機構は v1 で持たない

- `.rb` が唯一のソース。`.mrb` は Unity の ScriptedImporter が生成する sub-asset（`Library/` 内・**非コミット**・
  再 import で**自動再生成**）。
- novel-kit は自分の `.rb` を常に保持する前提（`.rb` を伴わないバイトコード単体の第三者配布はしない）。
- スキーマ変更（kwarg / コマンド名 / 糖衣メソッド名のリネーム、構造体変更）は **`.rb` 編集 + 再 import で追従**。
  バージョン番号・後方互換レイヤといった重厚な versioning は不要。
- Ruby 名のエイリアス（旧名→新名）は、大量リネームが痛くなった時の**任意の利便機能**（システム化はしない）。

## 2 種類の versioning を分離する（重要）

| | コマンドスキーマ versioning | シナリオ内容 versioning（save/replay 互換） |
|---|---|---|
| 対象 | C#↔Ruby の interface・`.mrb` | シナリオ本文の編集（行の増減・分岐変更） |
| 壊れるもの | `.mrb` バイトコード | 過去のセーブ／リプレイ履歴（選択/フラグ列） |
| 本決定 | **不要**（`.rb` 正・再生成で追従） | **別概念**・save-anywhere 実装時に対応 |

将来 save-anywhere をリプレイ式で実装する段では、記録した選択/フラグ履歴を `.rb` 再実行で再現するため、シナリオ編集で
行の index ずれ等が起き得る。これは **コマンドスキーマとは無関係**で、その save-anywhere 一括導入時に「履歴のシナリオ版数
チェック → 不一致なら近いチェックポイントへフォールバック」等で扱う（[実行モデル](/design/decisions/execution-model.md) は
現状そのリプレイ機構を持たず、前進専用 + チェックポイント割り切り）。

## トリップワイヤ

将来「`.rb` を伴わないバイトコード単体を第三者配布」する運用が生じたら、その時に初めて本格 versioning を導入する。

# 理由

- `.mrb` が常に `.rb` から再生成可能なので互換境界が軟らかく、自前運用では versioning は過剰。
- 2 つの versioning を分離することで、コマンド層を軽く保ちつつ save/replay の課題を save トピックへ正しく送れる。

# 帰結

- `.mrb` は成果物。Unity `Library/` 配下で非コミット（`.gitignore` 済み）。
- save/replay 互換は save-anywhere 実装時の課題として [残論点](/design/open-questions.md) に明記。

# 検討した代替案

- **スキーマバージョン番号 + エイリアス**: バイトコードを成果物として配布する前提の重厚版。自前運用では不要で不採用。
- **当面 versioning なし（区別もしない）**: シナリオ内容 versioning を見落とすと save/replay でバグるため、
  分離して明記する本決定を採る。
