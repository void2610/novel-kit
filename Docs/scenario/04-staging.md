# 4. 画面演出（立ち絵・背景）

## stage — 場面の宣言

画面に誰がどの位置で立つかは、`stage` で **レイアウト** と **キャスト** をまとめて宣言する。

```ruby
stage :single, [:alice]                    # 1 人構図
stage :pair,   [:alice, :bob]              # 2 人構図（左から順に配置）
stage :trio,   [:alice, :bob, :carol]      # 3 人構図
```

- レイアウト id の既定は `:single` / `:pair` / `:trio` / `:quad` / `:penta`（1〜5 人）。
  各位置（スロット）が画面のどこかはゲーム側の設定次第。プロジェクト固有のレイアウト
  （`:meeting` など）が追加されている場合もある。
- 配列で渡すと **並び順どおり** にスロット 0, 1, 2… へ割り当てる。
- 位置を明示したいときはハッシュ形式で書く:

```ruby
stage :trio, alice: 1, bob: 0, carol: 2    # alice を中央（slot 1）に
```

- キャストを省略すると、レイアウトだけ切り替えて全員退場に近い状態になる。
- すでに表示中のキャラは、新しい `stage` でも同じスロットならそのまま残る。
  スロットが変わるキャラは移動し、キャストから外れたキャラは退場する。

## portrait — 立ち絵の表示・表情替え

```ruby
portrait :alice, "smile"       # alice のスロットに立ち絵キー "smile" を表示
portrait :alice, "sad"         # 同じ場所で差し替え（表情替え）
```

- 使える立ち絵キーはキャラごとにゲーム側のカタログで決まる。一覧はプロジェクトで確認すること。
- 同じキャラに同じキーを続けて出しても何も起きない（演出の再発火はしない）。
- セリフと同時に表情を変えたいときは `say` の第 3 引数が使える（[2 章](./02-text.md)参照）:

```ruby
say "alice", "え……！", "alice/surprised"
```

> `stage` を宣言せずに `portrait` を呼ぶと、暗黙に 1 人構図（slot 0）として表示され、
> Console に警告が出る。1 人場面でも `stage :single, [:alice]` を書いておくのが行儀よい。
> キャストにいないキャラの `portrait` も slot 0 に出て警告になる（タイプミス検出のため）。

## exit_chara / clear_stage — 退場

```ruby
exit_chara :bob      # bob だけ退場
clear_stage          # 全員退場（レイアウトは維持）
```

> Ruby の予約語と衝突するため `exit` ではなく `exit_chara`。

## bg / still / image — 背景・一枚絵・補足画像

```ruby
bg "room"            # 背景を差し替え
still "ev_sunset"    # 一枚絵（イベント CG）を表示
image "map"          # 補足画像を画面中央に表示（立ち絵と同じ層）
hide_image           # 中央の補足画像を消す
```

使えるキーの一覧はゲーム側のカタログで決まる。

## メッセージウィンドウの制御

```ruby
hide_message_window   # ウィンドウごと隠す（一枚絵をフルで見せたいときなど）
still "ev_sunset"
wait 2.0
show_message_window   # 戻す

clear_message         # 表示中のセリフ・話者名だけ消す（ウィンドウは出したまま）
```

`clear_message` は場面転換で「直前のセリフが残ったまま背景だけ変わる」のを防ぐのに使う。

次章: [5. 音と演出効果](./05-audio-effects.md)
