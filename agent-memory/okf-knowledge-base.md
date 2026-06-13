---
name: okf-knowledge-base
description: ユーザーは各プロジェクトの知識を OKF (Open Knowledge Format) v0.1 で Knowledge~/ に管理する。規約は void2610/okf-conventions
metadata:
  node_type: memory
  type: reference
  originSessionId: 7d2d05f5-9d41-473c-835d-0227b7168db6
---

ユーザー（void2610）はプロジェクトの知識（設計・設定・システム）を **OKF (Open Knowledge Format) v0.1**
形式で `Knowledge~/` ディレクトリに管理する運用をしている。

- 規約リポジトリ: https://github.com/void2610/okf-conventions （`Knowledge~/conventions/` に **submodule・読み取り専用**で導入）。
- 導入は `okf-add.sh <project>`（submodule追加 + scaffoldコピー + CLAUDE.md に運用ルール追記）。
- 構成: `Knowledge~/{index.md, log.md, conventions/(submodule), design/, systems/, lore/}`。`~` で Unity import から除外。
- 1概念=1 Markdown。frontmatter の `type` 必須（Decision/Design/System/Reference/Character 等）。
- index.md/log.md は予約ファイル。**概念を作成/更新したら log.md に ISO日付で1行追記**。`timestamp` は更新時に現在時刻へ。
- リンクはバンドル基準絶対パス（`/design/...`）優先。`conventions/` 配下は編集しない。

**How to apply:** ユーザーのプロジェクトで `Knowledge~/` を読み書きする際は、まず
`Knowledge~/conventions/CONVENTIONS.md` を読んで従う。設計判断は `design/` に `type: Decision` で記録。
適用例: [[novel-kit-project]]。
