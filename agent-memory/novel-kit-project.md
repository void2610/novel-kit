---
name: novel-kit-project
description: novel-kit = 複数Unityプロジェクトのノベル機能を1つに統合する汎用ライブラリ。設計知識はリポジトリのOKF Knowledge~/design に永続化済み
metadata:
  node_type: memory
  type: project
  originSessionId: 7d2d05f5-9d41-473c-835d-0227b7168db6
---

`~/Documents/GitHub/novel-kit`（GitHub: void2610/novel-kit, **Private**）は、color-recollection /
unity1week-2026-03 / apocalyptic-apartment-hunting 等の個別ノベル実装を 1 つに統合する
汎用ノベルゲームライブラリ。**VContainer + VitalRouter.MRuby 前提**（スプレッドシートシナリオは対象外＝void-red 方式は無視）。

2026-06-13 時点で**設計フェーズ**（実装コードなし）。設計判断・機能棚卸し・残論点は
リポジトリ内 `Knowledge~/design/`（OKF形式, [[okf-knowledge-base]]）に永続化済み。新規セッションでは
まず `Knowledge~/design/index.md` と `decisions/index.md` を参照すること。

**重要なフレーミング**: novel-kit は「ゲーム内の一要素としてのノベルパート」エンジンであり、スタンドアロンVNではない。
進行（章/シナリオ選択/分岐/retry/resume）と**セーブは game 所有**。ライブラリは `PlayAsync(key,ct) -> NovelResult` で
1シナリオを再生するプリミティブに徹する（シーケンサ・シナリオ間gotoは持たない）。

**確定済み 16 件**（`Knowledge~/design/decisions/`、ユーザーとの議論で合意）。コア（実行モデル/コマンドスキーマ/
ルーター/テキストパイプライン/状態モデル/語彙）の骨格が決まり、**ランナーAPIを凍結できる状態**。主要: 実行モデル=リプレイ前提、
say最小+糖衣（id話者ハイブリッド）、ルーター=ノベル専用Router+DI市民、インラインタグ=TMP `<...>`単一・lexer所有・非タグエスケープ、
音声=voice除外/SE+BGM採用、versioning持たない(.rb正)、エフェクト=ハンドラawait統一、エラー=Faultedフェイルセーフ・サンドボックスv1無し。
**中優先まで全て解決済み**。未決は v1スコープ外のバックログ（ロールバック/途中再開/Novel.View依存スタンス等）のみ。

アーキ案: 4 asmdef（Novel.Commands/Runtime/View/Editor）。ADR・残論点の最新は repo を参照（このメモリは要約）。

**Why:** 複数セッションにまたがる長期プロジェクトで、設計合意が積み上がっている。
**How to apply:** 続きの議論・実装前に必ず repo の Knowledge~ を読み、決定を二重議論しない。
