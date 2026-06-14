---
type: Decision
title: DSL 語彙はリッチ統一語彙を常設
description: say/portrait/bg/still/se/... を常に利用可能にし、未配線コマンドは no-op デフォルトハンドラで握りつぶす。
tags: [decision, dsl, command, vitalrouter, versioning]
timestamp: 2026-06-15T00:00:00Z
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

## 実装で確定（2026-06-14, 実装レビュー後）

未配線コマンドの no-op は維持しつつ、「無言」をやめた。dev ビルド（Editor / Development Build）では「コマンドは来たが
対応 View が未供給」を一度だけ警告する no-op ファセット（`WarningPortraitView`/`WarningBackgroundView`/
`WarningAudioChannel`）を既定にした（本番は無音）。埋め込み用途で「なぜ出ないのか分からない」デバッグ地獄を避けるため
（[実装レビュー](/design/implementation-review.md) `NK-NOOP-SILENT`）。完全に黙らせたい game は Runtime の
`Null*` ファセットを登録すればよい。

## 実装で確定（2026-06-15, プロジェクト独自コマンドの拡張口）

常設リッチ語彙は **組込語彙**であって閉じた集合ではない。プロジェクト固有のコマンド（gameplay への作用・
独自演出等）を game が差し込めなければ、埋め込み用途の拡張性が成立しない。一方で独自コマンド 1 つを通すには
本来 3 箇所の配線が要る — ①語彙束縛（Ruby 名→C# コマンド型 `state.AddCommand<T>(name)`）、②ハンドラ写像
（`On(T)` を ノベル専用 Router へ `MapTo`）、③Ruby 糖衣（`def my_cmd; cmd :my_cmd, ... end`）。これらは
当初すべて `NovelScenarioRunner` 内に封印され、game から触れなかった。

**拡張口 `INovelCommandModule` を 1 つ開ける**ことで解決した（router-ownership の「既定を 1 つに決め切り、
必要が出たら後から足す」に沿う後付け）。

- `INovelCommandModule.RegisterVocabulary(MRubyState)` … ①。組込語彙の **後**に呼ばれ、`AddCommand<T>(name)` を追加する。
- `INovelCommandModule.MapHandlers(ICommandSubscribable) : IDisposable` … ②。実装が `[Routes]` を兼ね、生成済み
  `MapTo(router)` を返す。runner が `Dispose` でまとめて購読解除する。
- runner は `IEnumerable<INovelCommandModule>` を**集約注入**で受ける（VContainer は 0 件→空 / N 件→全件で解決。実機確認済み）。
  VContainer 配線は `builder.RegisterNovelCommand<TModule>()` 1 行（`Novel.VContainer`）。
- ③糖衣は **複数プリアンブル**で供給する。MRubyCS は実行時に Ruby ソースを eval できない（`.rb`→`.mrb` はインポート時
  コンパイル）ため、糖衣も別 `.rb`→`.mrb` を追加 `IPreambleSource` として登録する以外に道がない。runner は
  `IEnumerable<IPreambleSource>` を**登録順**に評価する（組込糖衣が先・game 追加糖衣が後）。`cmd :my_cmd, ...` を
  直書きすれば糖衣なしでも動くため、糖衣は任意。

組込未配線コマンドの no-op（上記）と独自コマンドの拡張は直交する。前者は「常設語彙の取りこぼし防止」、
後者は「語彙そのものの拡張」。語彙束縛とハンドラを 1 クラスに同居させることで、独自コマンドの定義箇所が
1 つにまとまり発見性を保つ（[実装レビュー](/design/implementation-review.md) `NK-PROJECT-CMD`）。

# 検討した代替案

- **最小コア + オプトインモジュール**（当初推奨）: 最小 game が未使用ハンドラを抱えない。可搬性より
  軽量性を優先する案だが、ユーザーは常設リッチ語彙を選好。不採用。
- **段階プリセット**（minimal/standard/full）: 選択肢が増える分、シナリオ可搬性が下がるため不採用。
- **語彙のみの薄い口 + ハンドラ写像は game 任せ**: `AddNovelVocabulary<T>(name)` だけ提供し、game が注入済み
  novel Router へ自前で `MapTo`。粒度は細かいが購読の破棄管理が game 持ちになり、独自コマンドの定義が
  語彙とハンドラに分散する。発見性と寿命管理を優先し `INovelCommandModule` に束ねる方を採用。
