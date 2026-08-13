---
type: Decision
title: 構図プレビュー — IPortraitChannel を編集モードで駆動する
description: 立ち絵の構図調整で Play を往復しないよう、エディタ窓が game 実装の IPortraitChannel をそのまま編集モードで呼ぶ。座標は引き続き game が持ち、novel-kit は位置を知らない。実装側には「非再生時は即座に反映する」契約を課す。
tags: [decision, editor, portrait, stage, layout, preview, tooling]
timestamp: 2026-08-14T00:00:00Z
status: 確定
---

# 状況

slot 座標は game が持ち（[プロジェクトリファレンス](/design/decisions/project-reference.md) のとおり novel-kit は
構図 id とスロット数しか知らない）、実行時に `SwitchLayoutAsync` で初めて適用される。
そのため座標を詰める作業が「値を直す → Play → 見る → Stop」の往復になっていた。
プロジェクトリファレンスの構図タブが描くミニ図はスロット数からの等間隔模式図で、実際の座標ではない。

# 決定

## エディタ窓が game 実装をそのまま編集モードで呼ぶ

`Novel/Stage Preview` は開いているシーンから `IPortraitChannel` を見つけ、`EnumerateLayouts()` の構図と
指定したスプライトで `SwitchLayoutAsync` / `ShowAsync` / `HideAsync` を**編集モードのまま**呼ぶ。

## 位置は引き続き game が持つ（novel-kit に座標を持たせない）

構図の座標は画面設計そのもので game ごとに違うため、novel-kit が持つと不適切な既定を押し付ける。
プレビューを「専用の描画経路」ではなく実装の呼び出しにしたのは、**本番と同じ経路を通す**ため。
別経路を用意すると本番と乖離し、プレビューで合わせたのに再生すると違う、が起こる。

## 実装への契約: 非再生時は即座に反映する

編集モードでは PlayerLoop が回らないため、アニメーション完了を待つ実装は永久に完了しない
（LitMotion は編集モード用スケジューラを Editor アセンブリにしか持たない）。
そこで **`Application.isPlaying` が false のときは待たずに最終状態へ飛ばす**ことを実装側の契約とする。
窓はその場で完了しなかった呼び出しを検出して警告を出すので、契約違反は沈黙しない。

この契約はプレビュー専用の抽象を増やさない。`IPortraitChannel` に編集モード用メソッドを足す案も出たが、
本番と別経路になるうえ実装の重複を生むため採らなかった。

# 影響

- 座標調整が Play 往復なしで完結する
- `EnumerateLayouts()` を既定のまま（標準 5 構図）にしている実装は、実在しない構図が窓に並ぶ。
  実際に定義した構図を返すべきという圧力がかかる（沈黙の不一致が見えるようになる）
- プレビューはシーン上のオブジェクトを直接書き換える。座標は再生時に構図から再適用されるため、
  保存せず閉じても実害はない
