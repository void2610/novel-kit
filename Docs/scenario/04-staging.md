# 4. 画面演出（立ち絵・背景）

## stage — だれがどこに立つかを決める

画面に立つキャラの人数と位置は、`stage` でまとめて指定する。
**構図の名前** と **立つキャラの並び** を書く。

```ruby
stage :single, [:alice]                    # 1 人構図。alice が立つ
stage :pair,   [:alice, :bob]              # 2 人構図。書いた順に左の位置から並ぶ
stage :trio,   [:alice, :bob, :carol]      # 3 人構図
```

- 構図の名前は標準で `:single` / `:pair` / `:trio` / `:quad` / `:penta`（1〜5 人）が使える。
  それぞれの立ち位置が画面のどこになるかはゲーム側の設定次第。プロジェクト独自の構図
  （`:meeting` など）が用意されていることもある。**このプロジェクトで使える構図の一覧は
  Unity のメニュー Novel > Project Reference で確認できる。**
- キャラは `[ ]` の中に並べた順で、位置 0、位置 1、位置 2 … に割り当てられる。
- 並び順ではなく位置を名指ししたいときは、`キャラ名: 位置番号` の形で書く:

```ruby
stage :trio, alice: 1, bob: 0, carol: 2    # alice を中央（位置 1）に
```

`stage` を続けて書いたときの動きは自然になるようにできている:
すでに表示中のキャラは同じ位置ならそのまま残り、位置が変わるキャラは移動し、
新しい指定に含まれないキャラは退場する。キャラを省いて `stage :pair` とだけ書くと、
構図だけ切り替えて全員退場に近い状態になる。

## portrait — 立ち絵を出す・表情を変える

```ruby
portrait :alice, "smile"       # alice の位置に立ち絵「smile」を表示
portrait :alice, "sad"         # 同じ場所で差し替え（表情替え）
```

- **使える立ち絵の名前はキャラごとにゲーム側の設定で決まる**。一覧は Unity のメニュー
  **Novel > Project Reference** で確認できる。
- 同じキャラに同じ立ち絵を続けて指定しても、何も起こらない（出直し演出は再生されない）。
- セリフと同時に表情を変えたいときは、`say` の 3 つ目に立ち絵の名前を書く方法もある
  （[2 章](./02-text.md)参照）:

```ruby
say "alice", "え……！", "alice/surprised"
```

> `stage` を書かずにいきなり `portrait` を使うと、1 人構図とみなして一応表示されるが、
> Unity の Console に警告が出る。1 人の場面でも `stage :single, [:alice]` を書いておくのがおすすめ。
> `stage` に並べていないキャラの `portrait` も、位置 0 に表示されたうえで警告になる
> （キャラ名の書き間違いに気づけるようにするため）。

## exit_chara / clear_stage — 退場させる

```ruby
exit_chara :bob      # bob だけ退場
clear_stage          # 全員退場（構図はそのまま）
```

> 「exit」ではなく `exit_chara`。`exit` は別の意味に予約されている言葉のため使えない。

## bg / still / image — 背景・一枚絵・補足画像

```ruby
bg "room"            # 背景を「room」に差し替え
still "ev_sunset"    # 一枚絵（イベント CG）を表示
image "map"          # 補足画像（地図・手紙など）を画面中央に表示
hide_image           # 中央の補足画像を消す
```

使える画像の名前はゲーム側の設定で決まる（一覧は **Novel > Project Reference**）。

## メッセージウィンドウを隠す・戻す

一枚絵を画面いっぱいに見せたいときなどは、メッセージウィンドウごと隠せる。

```ruby
hide_message_window   # ウィンドウごと隠す
still "ev_sunset"
wait 2.0              # 2 秒見せる
show_message_window   # ウィンドウを戻す
```

ウィンドウは出したまま、表示中のセリフと話者名だけを消すこともできる。
場面転換のときに直前のセリフが残ってしまうのを防ぐのに使う。

```ruby
clear_message
bg "street"
```

次章: [5. 音と演出効果](./05-audio-effects.md)
