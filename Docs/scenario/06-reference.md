# 6. 命令早見表

使える命令とタグの一覧。書き方は「例」の列をそのまま真似すればよい。
詳しい説明は各章を参照。

## 命令一覧

### テキスト（[2 章](./02-text.md)）

| 例 | 意味 |
|---|---|
| `chara :alice` | キャラの登場宣言。以降 `alice "…"` でセリフを書ける |
| `alice "こんにちは。"` | alice のセリフ（`chara` 宣言後） |
| `alice "……。", as: "？？？"` | この行だけ表示名を「？？？」にする |
| `say "alice", "こんにちは。"` | セリフの別の書き方（上と同じ意味） |
| `say "alice", "え！", "alice/odoroki"` | セリフと同時に立ち絵「alice/odoroki」へ切り替え |
| `narration "——朝が来た。"` | 地の文（話者を空欄にした `say` と同じ。`say "——朝が来た。"` でも可） |

### 選択肢・フラグ（[3 章](./03-branching.md)）

| 例 | 意味 |
|---|---|
| `n = choose(["はい", "いいえ"])` | 選択肢を出し、選んだ番号（1 つ目 = 0）を n に覚える |
| `choose(["A", "B"], key: :picked)` | 選んだ番号をフラグ `picked` に記録する（セーブに残る） |
| `flag :met_alice` | フラグ `met_alice` に 1 を入れる（印を立てる） |
| `flag :score, 5` | フラグ `score` に 5 を入れる（整数のみ） |
| `val(:score)` | フラグ `score` の中身を読む（未使用なら 0） |
| `narration "スコアは%{score}点"` | 文中にフラグの値を差し込む（テキスト変数。`#{}` は使わない → [7 章](./07-pitfalls.md)） |
| `flag?(:met_alice)` | 印が立っているか（0 以外か）を調べる |

### 画面（[4 章](./04-staging.md)）

| 例 | 意味 |
|---|---|
| `stage :pair, [:alice, :bob]` | 2 人構図。書いた順に左から並ぶ（構図: `:single`〜`:penta` = 1〜5 人） |
| `stage :trio, alice: 1, bob: 0, carol: 2` | 位置番号を名指しで割り当てる形 |
| `portrait :alice, "smile"` | alice の立ち絵を「smile」にする（表示・表情替え） |
| `exit_chara :bob` | bob を退場させる |
| `clear_stage` | 全員退場（構図はそのまま） |
| `bg "room"` | 背景を「room」に差し替え |
| `still "ev_sunset"` | 一枚絵（イベント CG）を表示 |
| `image "map"` | 補足画像を画面中央に表示 |
| `hide_image` | 中央の補足画像を消す |
| `hide_message_window` | メッセージウィンドウを隠す |
| `show_message_window` | メッセージウィンドウを戻す |
| `clear_message` | 表示中のセリフ・話者名だけ消す（ウィンドウは残す） |

### 音・効果・その他（[5 章](./05-audio-effects.md)）

| 例 | 意味 |
|---|---|
| `bgm "daily"` | BGM を流す・切り替える |
| `bgm ""` | BGM を止める |
| `se "door_open"` | 効果音を 1 回鳴らす |
| `se_loop "knock", 0.5, 3` | 0.5 秒おきに 3 回鳴らす（省略時 0.5 秒 × 3 回） |
| `wait 1.5` | 1.5 秒待つ |
| `shake 2.0` | 画面を揺らす。数字は強さ（省略時 1.0）※要ゲーム側対応 |
| `flash 0.2` | フラッシュ。数字は秒数（省略時 0.2）※同上 |
| `fade_out 1.0` / `fade_in 1.0` | フェードアウト/イン。数字は秒数（省略時 1.0）※同上 |
| `blackout` | 一瞬で真っ暗にする ※同上 |
| `world_effect :zoom, 1.5` | プロジェクト定義の画面効果を名前で呼ぶ ※同上 |
| `cinematic :flashback` | 用意された演出を名前で始める ※要ゲーム側対応。名前は Project Reference の「演出」タブ |
| `cinematic_stop :flashback` | その演出を終える |
| `cmd :screen_shake, power: 2.0` | プロジェクト独自の命令を直接呼ぶ |

## 文中タグ一覧（[2 章](./02-text.md)）

セリフ・地の文の中に埋め込む。

| 例 | 効果 |
|---|---|
| `\n` | その位置で改行する |
| `<w=0.4>` | その位置で 0.4 秒止まる |
| `<p>` | その位置でクリック待ち |
| `<speed=2x>…</speed>` | 囲んだ部分の表示速度（`2x` = 2 倍速、`0.5` = 半分） |
| `<fast>` | 以降を一気に表示 |
| `<shake>…</shake>` | 囲んだ文字が揺れる |
| `<wave>…</wave>` | 囲んだ文字が波打つ |
| `<ruby=よみ>漢字</ruby>` | ふりがな |
| `<color=#8cf>…</color>` | 文字色 |
| `<size=120%>…</size>` | 文字の大きさ |
| `<b>…</b>` / `<i>…</i>` / `<u>…</u>` | 太字 / 斜体 / 下線 |
| `<noparse>…</noparse>` | 囲んだ部分をタグ扱いせずそのまま表示 |

上記のほか、Unity のテキスト表示機能（TextMeshPro）のタグ
（`<s>` `<sup>` `<sub>` `<mark>` `<link>` `<sprite>` `<font>` `<align>` など）もそのまま使える。
タグ以外の `<` は普通の文字として表示される。

## ふりがな辞書（`Resources/Novel/ruby.rb`）

| 例 | 意味 |
|---|---|
| `ruby '異能', 'いのう'` | 出てくるたびに毎回ふりがなを振る |
| `ruby '主人公', 'しゅじんこう', :first` | 最初の 1 回だけ振る |

次章: [7. 注意点とよくある質問](./07-pitfalls.md)
