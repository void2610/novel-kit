---
type: Decision
title: MRuby ランタイムエラー処理・サンドボックス
description: シナリオ実行を try/catch で包み backtrace を surface。リリースはフェイルセーフで Faulted 終了。サンドボックスは v1 無し（一次コンテンツ前提）。
tags: [decision, mruby, error-handling, sandbox, runner, result]
timestamp: 2026-08-28T00:00:00Z
status: 確定
---

# 状況

調査した全プロジェクトで `state.ExecuteAsync` 周りに try/catch が無く、`.rb` の nil 呼び 1 つで未処理例外になり
行番号も出ない。他者（および将来の自分）に渡すライブラリとしては不可。

# 決定

## エラー捕捉と報告

- シナリオ実行（`state.ExecuteAsync`）を try/catch で包み、`MRubyRaiseException`（および一般例外）を捕捉。
  **シナリオキー + Ruby バックトレース/行**を抽出して surface する。
- Fiber の性質上、例外は Ruby スタックを巻き戻すため**行スキップ継続は非現実的** → シナリオは fault で中断して終わる。
- `NovelResult` に **`Faulted(errorInfo)`** を追加（`Completed` / `Cancelled` / `Faulted`）。

## 開発時 vs リリース時

- **開発（Editor / dev build）**: 「シナリオ名 + Ruby 行 + backtrace」をログ。任意で**画面上エラーオーバーレイ**
  （著者が即気づける）。テスト時は例外を throw して失敗扱いにできる。
- **リリース**: **フェイルセーフ**。シナリオを `Faulted` で終了し game に返すだけで、ゲーム全体は落とさない。
  進行は game 所有（[フロー/シーケンサの境界](/design/decisions/flow-boundary.md)）なので、game が `Faulted` を見て復帰を決める。
- 挙動を選べる **`INovelErrorHandler`（注入ポリシー）** を用意し、ビルド種別で既定を出し分ける
  （Editor = throw / overlay、Release = 報告 + `Faulted`）。

## サンドボックス / 信頼境界

- シナリオ `.rb` は**開発者自身の一次コンテンツ**で、ゲームコードと同じ信頼レベル（UGC ではない）。
- MRubyCS は本来ホスト機能（FS / ネットワーク）を持たず**バインドしたものしか呼べない**。ノベルでは
  コマンド DSL + preamble + 状態アクセスしか公開しないため、**自然に限定**される。
- → **v1 は重いサンドボックスを入れない**（一次コンテンツ前提・MRubyCS の自然な限定に依存）。

# 理由

- 他者/将来の自分に渡すライブラリは、エラーを位置情報付きで surface し、フェイルセーフであるべき。
- 一次コンテンツ前提ならサンドボックスは過剰。MRubyCS の自然な限定で足りる。

# 帰結

- `NovelResult = Completed | Cancelled | Faulted(errorInfo)`。`Faulted` の復帰は game 所有。
- `INovelErrorHandler` を DI で注入（既定実装をビルド種別で提供）。
- 「UGC シナリオを許す」用途は将来のハードニング課題として [残論点](/design/open-questions.md) のバックログに記載。

## 実装で確定（2026-06-14, 実装レビュー後）

`INovelErrorHandler.OnScenarioFaulted` の引数を生 `Exception` から `NovelErrorInfo{ScenarioKey, Message, Detail,
Exception}` へ拡張し、runner が `MRubyRaiseException.GetBacktraceString()`（リフレクションでバージョン差を吸収）で
Ruby backtrace を `Detail` に surface する。既定ハンドラを無音の `NullErrorHandler` から、dev ビルドで
`Debug.LogError` する `DebugNovelErrorHandler`（View 層）へ変更した（明示的に黙らせたい game は `NullErrorHandler`
を登録）。当初は既定が無音で作家にエラーが一切届かなかった（[実装レビュー](/design/implementation-review.md)
`NK-ERROR-SILENT`）のを解消。`NovelResult` は enum のまま（情報はハンドラ経由で運ぶ）。

## 実装で確定（2026-08-28, 無言失敗の一掃）

- **Ruby backtrace は一度も出ていなかった**。`GetBacktraceString()` を「例外型」から探していたが、
  実際は `MRubyState` のメソッドで、リフレクション検索が常に空振りして C# 例外文字列へフォールバックしていた
  （2026-06-14 の「実装で確定」は誤った追認だった）。正しい経路は
  `例外.ExceptionObject.Backtrace.ToString(state)`。`MRubyState.GetBacktraceString()` は VM を抜けた後では
  空を返すため使えない。バージョン差の吸収という当初方針どおりリフレクションのまま辿り直した。
- **`.rb` の行番号は現状の依存では出せない**。`jp.hadashikick.mrubycs-compiler` の `MrbcsCompile` は
  ソースとバイト列しか受け渡さず、ファイル名も debug info も渡す口が無いため、backtrace は
  `raise in byte sequence: 0` の形にしかならない（複数行スクリプトでも行番号 0 を実測）。
  代替として `NovelErrorInfo.SayNumber` / `LastSayText`（落ちる直前に処理した say の通し番号と原文）を
  位置の手掛かりに載せた。原文は resolve / 変数展開の前・タグ込みで持つ（訳文や展開後だと `.rb` の記述と
  一致せず検索できないため）。通番より原文の方が実用的で、これで `.rb` を検索すれば該当行に辿り着ける。
  行番号を出すにはコンパイラパッケージ側の対応が要る（[残論点](/design/open-questions.md) 送り）。
- **Core の既定ハンドラが無音だった**。2026-06-14 の「既定を DebugNovelErrorHandler へ変更した」は
  View ヘルパにしか適用されておらず、`RegisterNovelKitCore` は `NullErrorHandler` のままだった。
  `DebugNovelErrorHandler` を `Novel.View` から `Novel.Runtime` へ移し（Runtime は UnityEngine を参照できる）、
  Core の既定に据えて ADR の意図と実装を一致させた。
- **例外にならない不具合に `OnRuntimeIssue` を追加**。「シナリオが引けない」「画像キーが解決できない」は
  例外ではないため `OnScenarioFaulted` の意味と合わず、かといって黙ると原因が掴めない
  （とくに画像キーは無言で空表示になり「立ち絵が出ない」の調査が手詰まりだった）。
  `NovelIssueInfo{Kind, ScenarioKey, Subject, Message}` を default 実装付きメソッドで通知する
  （音キー・構図の列挙で default を置かなかったのとは逆の判断。あちらは「実装忘れ = 沈黙」だったが、
  こちらは未実装でもライブラリが dev ログを出すため沈黙しない）。dev ログとハンドラ通知は
  `NovelDiagnostics` に集約し、検知点が増えても報告の作法がぶれないようにする。
- **シナリオ未発見は `Completed` ではなく `Faulted`**。「一瞬で正常終了」は最も原因を掴みにくい失敗で、
  game 側も成功と誤認する。破壊的変更だが、無言失敗を残すより優先した。

# 検討した代替案

- **行スキップ継続**: Fiber が例外で巻き戻るため非現実的。不採用。
- **ハードクラッシュ（現状）**: 埋め込みノベルでゲーム全体が落ちるのは不可。不採用。
- **本格サンドボックス（capability 制限）**: 一次コンテンツでは過剰。v1 不採用。
