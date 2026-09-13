---
type: Decision
title: コマンド糖衣の自動生成 — 語彙から preamble .rb をエディタ生成
description: RegisterNovelCommandSugars() でコマンド語彙から糖衣 preamble を自動生成する。実行時 eval 不可のため「エディタの DI ビルド時に .rb を生成 → インポータが .mrb 化 → IPreambleSource として読む」形。位置 + キーワード両対応・衝突は生成スキップで手書き常勝。
tags: [decision, dsl, preamble, sugar, codegen, editor, writer]
timestamp: 2026-09-05T00:00:00Z
status: 確定
---

# 問題

`cmd :screen_shake, power: 2.0` は語彙登録だけで書けるが、基本コマンドを裸 (`screen_shake 2.0`) で
書かせたいプロジェクト (color-recollection 等、コマンド数が多い) では、引数をそのまま渡すだけの
糖衣 def を全コマンド分手書きすることになり重複記述になる。

# 決定

`builder.RegisterNovelCommandSugars()` (Novel.View.VContainer) の明示 1 行で有効化する。

- **生成経路**: MRubyCS は実行時に Ruby ソースを eval できないため、エディタの DI ビルド時
  (再生開始時) に記録用 `INovelVocabulary` で語彙をキャプチャし (`NovelCommandSugars.Publish` →
  `Novel.Editor` の `CommandSugarFileWriter` が購読)、`Assets/Resources/Novel/CommandSugars.rb` を
  生成する。ScriptedImporter が `.mrb` 化し、同じ Register が `IPreambleSource` として読む。
  差分があるときだけ上書きし、git にコミットされる前提。**新コマンドの糖衣が効くのは次の再生から**
  (Project Reference と同じ「一度再生」制約)。
- **生成形**: `def name(a = nil, b = nil, **kw)` — 宣言順の位置引数 (全省略可) + キーワード委譲。
  nil でない引数だけを hash に積んで `cmd :name, **h`。未指定はデシリアライザの C# 既定値に任せる
  (C# の init 既定値はリフレクションで読めないため Ruby 側既定値には反映しない、で辻褄が合う)。
  MRubyCS の `**` スプラットは def 側・呼び出し側とも動作することを実測済み。
  `[NovelDescription]` を def 直上コメントとして出力し、Project Reference の糖衣説明に流用する。
- **衝突方針**: 組込語彙 (`NovelCommandSugars.BuiltinCommandNames`、runner の AddCommand と対で更新) と
  組込 preamble の def 名 (ソースを `RubyDefParser` で読む) に一致したら生成スキップ + 警告。
  生成 preamble の登録位置は「組込の直後・game 追加分の前」に固定し、**手書き糖衣が常に後勝ち**で
  上書きできる (賢い上書きはしない、の方針)。重複登録・Ruby 識別子にできない名前もスキップ。
  引数名が識別子にできないコマンドは位置引数を諦めて `**kw` 委譲のみ生成する。

# 検討した代替案

- **アセットインポート時フックで生成**: 当初ユーザー選択だったが、語彙はコンストラクタ依存を持つ
  モジュールを DI が組んで初めて読める (型走査インスタンス化は project-reference ADR で不採用済み) ため、
  インポート時には「新コマンド」を知る手段がなく、古いキャプチャの再出力しかできないと判明して DI ビルド時へ変更。
- **実行時に C# から DefineMethod**: 生成物ゼロで済むが、VitalRouter の `cmd` (fiber yield) の再実装か
  内部委譲が要り密結合になる。エディタ一覧・Validate にも「生成糖衣」の新概念を教える必要があり不採用。
  .rb 生成なら既存の糖衣表示・検証経路にそのまま乗る。
- **キーワードのみ / 位置のみの生成形**: 位置 + キーワード両対応が書き味と安全性を両立するため不採用。
