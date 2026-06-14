---
type: Decision
title: MRuby ランタイムエラー処理・サンドボックス
description: シナリオ実行を try/catch で包み backtrace を surface。リリースはフェイルセーフで Faulted 終了。サンドボックスは v1 無し（一次コンテンツ前提）。
tags: [decision, mruby, error-handling, sandbox, runner, result]
timestamp: 2026-06-13T00:00:00Z
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

# 検討した代替案

- **行スキップ継続**: Fiber が例外で巻き戻るため非現実的。不採用。
- **ハードクラッシュ（現状）**: 埋め込みノベルでゲーム全体が落ちるのは不可。不採用。
- **本格サンドボックス（capability 制限）**: 一次コンテンツでは過剰。v1 不採用。
