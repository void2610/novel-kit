---
type: Decision
title: DSL 語彙はリッチ統一語彙を常設
description: say/portrait/bg/still/se/... を常に利用可能にし、未配線コマンドは no-op デフォルトハンドラで握りつぶす。
tags: [decision, dsl, command, vitalrouter, versioning]
timestamp: 2026-06-13T00:00:00Z
status: 確定
---

# 状況

MRuby コマンド語彙はプロジェクトごとに差がある（U1W は 10+、AAH は 2、CR は 6）。
ライブラリの著者向けコマンドセットをどう揃えるかで、最小 game の負担と表現 game の自由度が変わる。

# 決定

**リッチ統一語彙を常設**する。

- `say/narration/choose/flag/portrait/bg/still/se/wait/...` を常に利用可能にする。
- ハンドラ未配線のコマンドは **no-op デフォルトハンドラ**で握りつぶし、`.rb` が常に動くことを保証する
  （game 側 `NovelLifetimeScope` で未配線コマンドの no-op デフォルトを自動登録する。これは実装方針として確定）。

```
全コマンド常設(say/portrait/bg/still/se/...)
未配線 → no-op
フル表現を常に記述可 / 最小 game も全部同梱
```

# 理由

- 著者はゲームに関わらず一貫した語彙でシナリオを書け、シナリオの可搬性が上がる。
- ユーザー判断（最小コア + オプトインより、常設リッチ語彙を選好）。

# 帰結（重要: 互換境界が広がる）

- 全コマンドを常設＝`[MRubyObject] record struct` が `.mrb` バイトコードに焼かれる
  **互換境界が広く**なる。一度 `SayCommand{Speaker,Text,VoiceId}` 等に対してコンパイルされた `.mrb` は、
  フィールド追加/リネーム/コマンド名変更で静かに壊れる。
- したがって **コマンドスキーマのバージョニング/エイリアス戦略**が必須になる
  （別ラウンドで設計予定 → [残論点](/design/open-questions.md)）。
- enum は snake_case 文字列で受け、単一の `ParseSymbol<T>` で解決する（[アーキテクチャ](/design/architecture.md)）。
- コマンド名規約（`say` か `speak` か、`[Routes]` 規約か `[Route]` per-method か）は要確定
  → [残論点](/design/open-questions.md)。

# 検討した代替案

- **最小コア + オプトインモジュール**（当初推奨）: 最小 game が未使用ハンドラを抱えない。可搬性より
  軽量性を優先する案だが、ユーザーは常設リッチ語彙を選好。不採用。
- **段階プリセット**（minimal/standard/full）: 選択肢が増える分、シナリオ可搬性が下がるため不採用。
