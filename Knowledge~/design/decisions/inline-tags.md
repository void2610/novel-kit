---
type: Decision
title: 行内インラインタグ言語を v1 実装
description: maxVisibleCharacters 直制御から字句解析ベースのタイプライタへ再設計し、行内の pause/速度/部分スタイルを可能にする。
tags: [decision, text, typewriter, tags]
timestamp: 2026-06-13T00:00:00Z
status: 確定
---

# 状況

既存実装のテキスト効果は行単位変換のみ（ルビ・キーワード markup・`ProductionSubtitleStyle` enum）。
`say()` 内で途中ポーズ・速度変更・部分着色・shake ができない。これは VN タイプライタの table-stakes
（Ren'Py の `{w}{p}{fast}`、Naninovel のインラインタグ）であり、全プロジェクトに欠落している。
現行の color-recollection は `maxVisibleCharacters` アキュムレータ方式で、途中ポーズ/再スタイルができない。

# 決定

**行内インラインタグ言語を v1 で実装**する。

- `{w=0.5}`（待機）/ `{speed=2x}` / 色・サイズ span / `{shake}...{/shake}` / インラインルビ 等を
  サポート。
- タイプライタを `maxVisibleCharacters` 直制御から、**字句解析（パーサ → トークン列 → Reveal 中に
  pause/restyle）** ベースへ再設計する。

```
say "待って…{w=0.5}本気なの{shake}？{/shake}"
パーサ → トークン列 → Reveal 中に pause/restyle
```

# 理由

- 行内制御は VN として必須の表現力で、後付けはエンジン契約の作り直しになるため最初から入れる。
- 字句解析式なら TMP リッチテキストタグと独自タイミングタグを分離して扱え、可変長の途中挿入にも対応。

# 帰結

- タイプライタエンジンの契約は「トークン列を逐次 Reveal し、タイミング/スタイルトークンで
  pause/restyle する」形になる。`Novel.Runtime` の純 C# 状態機械（void-red `TextProgressController` 形）
  + TMP 技法（CR の deltaTime アキュムレータ、タグを文字数にカウントしない扱い）を統合する。
- タグ構文の正規定義（記法・パースを Runtime で行うか Ruby 文字列補間で表すか）は要詳細設計
  → [残論点](/design/open-questions.md)。
- ルビ/clickable markup は行内タグ体系に取り込みつつ、JP/ゲーム固有部分は任意モジュール化する。

# 検討した代替案

- **行単位スタイル属性のみ**（`say "...", style: :impact`）: 最小だが行途中の制御不可・後付け困難。不採用。
- **既存 markup 踏襲のみ**（whole-line のルビ/キーワード）: インライン制御なし。不採用。
