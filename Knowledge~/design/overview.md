---
type: Design
title: novel-kit 概要
description: VContainer + VitalRouter.MRuby 前提の汎用ノベルゲームライブラリの目的・スタック・調査対象・スコープ。
tags: [overview, scope, novel]
timestamp: 2026-06-13T00:00:00Z
---

# 目的

void2610 の複数 Unity プロジェクトで個別実装されていたノベルゲーム機能を、
1 つの再利用可能なライブラリ `novel-kit` へ統合する。各ゲームが自前で書いていた
メッセージ送り・選択肢・立ち絵・既読/バックログ・MRuby シナリオ実行などを、
共通の Runtime + コマンド語彙 + View 抽象として切り出す。

# 前提スタック（確定）

| 役割 | パッケージ |
|---|---|
| DI | VContainer |
| コマンドバス | VitalRouter |
| シナリオスクリプト | VitalRouter.MRuby (MRubyCS / MRubyCS.Serializer) |
| 非同期 | UniTask |
| リアクティブ | R3 |

# スコープ

- **位置づけ**: **ゲーム内の一要素としてのノベルパート**エンジン。スタンドアロン VN ではない。
  進行（章/フェーズ・シナリオ選択・分岐・retry・resume）は game 側の責務で、ライブラリは命令された
  1 シナリオを再生するプリミティブに徹する → [フロー/シーケンサの境界](/design/decisions/flow-boundary.md)。
- **対象**: MRuby (`.rb` → `.mrb` バイトコード) で記述するシナリオの実行と提示。
- **対象外**: スプレッドシート / CSV / Excel によるシナリオ読み込み（void-red 方式）。
  提示層のアイデアのみ参考にし、ローダ機構は取り込まない。シーケンサ・シナリオ間 `goto` も持たない。

# 調査対象プロジェクト（2026-06-13 時点）

`/Users/shuya/Documents/GitHub` 配下の Unity プロジェクトを横断調査した。詳細は
[機能棚卸し](/design/feature-inventory.md) を参照。

| プロジェクト | スタック | 役割 |
|---|---|---|
| color-recollection | VContainer + VitalRouter.MRuby + R3 | リファレンス基盤（最も完全な専用ノベルシステム） |
| unity1week-2026-03 | VContainer + VitalRouter.MRuby + VitalRouter.R3 | DI 統合の規範・演出/音声語彙の宝庫 |
| apocalyptic-apartment-hunting | VContainer + VitalRouter.MRuby（最新導入） | 2 ルーター構成・入力/ゲート抽象 |
| void-red / the-garden-of-garden-gnome / otajam-2025-11 | VContainer + R3（非 MRuby） | 提示層アイデアバンク（DSL 対象外） |

# 設計の進め方

- 既存実装の調査 → 統合機能の棚卸し → ユーザーとの議論で設計判断を確定、という順で進行。
- 確定した意思決定は [decisions](/design/decisions/index.md) に ADR として 1 件 1 ファイルで記録。
- 未決事項は [残論点](/design/open-questions.md) に集約し、ランナー API 凍結前に解消する。

# 確定済みの主要判断（要約）

詳細は各 ADR を参照。

1. [実行モデル](/design/decisions/execution-model.md): リプレイ前提設計（入力ログ + `.rb` 再実行）。
2. [ライブラリ範囲](/design/decisions/library-scope.md): インターフェースコア + 参考 View 別パッケージ。
3. [インラインタグ](/design/decisions/inline-tags.md): 行内タグ言語を v1 実装（字句解析ベースのタイプライタ）。
4. [DSL 語彙](/design/decisions/dsl-vocabulary.md): リッチ統一語彙を常設（未配線は no-op）。
5. [決定性コントラクト](/design/decisions/determinism-contract.md): 当面は後回し（履歴記録のみ先行）。
6. [キャラクターモデル](/design/decisions/character-model.md): 単一スプライト差し替え。
7. [ローカライズ](/design/decisions/localization.md): 日本語のみ + 抽出フック。
8. [状態モデル](/design/decisions/state-model.md): 単一 `IStateStore` に統一・`choose()` はユニークキー割当。
