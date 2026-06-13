---
type: Decision
title: 音声スコープ — voice は v1 除外・SE/BGM は v1 採用
description: キャラ音声(VoiceService)は v1 対象外で将来は別コマンド+糖衣。効果音/BGM は v1 に含める。
tags: [decision, audio, voice, se, bgm, scope, command]
timestamp: 2026-06-13T00:00:00Z
status: 確定
---

# 状況

unity1week に堅牢な ID 式 `VoiceService`（id→cue→variant、anti-repeat、cooldown、fade。ただし START/STOP のみで
auto 同期なし）と `play_se`。void-red に名前付き SE 表（random pitch / dB volume / PlaySe が clip 長を返す）+
template の `BgmManager`/`SeManager`。音声系をどこまで v1 に入れるかを決める。

# 決定

## キャラ音声（voice）は v1 対象外

- voice 機能（voice_id / `VoiceService`）は**現段階では実装しない**。
- 将来実装する場合は **`say` に付けず、独立 `voice` コマンド + 各プロジェクトの preamble 糖衣で吸収**する。
  これは純粋に加算的で既存 `say`・既存 `.rb` を一切触らない＝**非破壊**（`.mrb` の互換境界は Ruby 表層の
  コマンド名/メソッド名/kwarg 名であり、新コマンド追加はそこに干渉しない）。
- したがって `SayCommand` は voice フィールドを持たない（[コマンド名規約と say スキーマ](/design/decisions/command-schema.md)
  の VoiceId slot 予約を撤去）。
- 行と音声の紐付け（atomicity）は糖衣が `voice(:x); say(...)` で束ねればよく、音声考慮の auto 待ち
  （行が音声終了を待つ）は実行時/設定の話。どちらも将来追加時にシナリオ書き換え不要。

```ruby
# 将来 voice を足すときの一例（プロジェクト糖衣で吸収）
def alice(text, voice: nil) ; voice(voice) if voice ; say(:alice, text) ; end
```

## SE / BGM は v1 採用

- `se`（効果音・ワンショット）と `bgm`（楽曲・ループ/フェード）をコマンドとして **v1 に含める**。
- 素材: void-red の名前付き SE 表（random pitch / dB volume / clip 長返却）+ template の `BgmManager`/`SeManager`、
  unity1week の `play_se`。
- コアは `IAudioChannel`（SE/BGM 再生の抽象）を定義し、ハンドラは**任意解決・未登録なら no-op**
  （[DSL 語彙](/design/decisions/dsl-vocabulary.md) の「リッチ語彙・未配線 no-op」方針）。本格実装は `Novel.View`
  もしくは任意モジュール。
- `IAudioChannel` は SE の clip 長を返せる形にし、auto モードで「SE を鳴らし終えるまで待つ」挙動に使えるようにする
  （void-red のアイデア）。

# 理由

- voice はフルボイス前提の重い機能で多くの作品で不要。除外で v1 を軽くし、別コマンド方式で後から非破壊に足せる。
- SE/BGM は VN として基本の演出で、素材も既存実装に揃っている。

# 帰結

- `SayCommand` は最小（`SpeakerId` / `DisplayAs?` / `Text`）。voice フィールドなし。
- `se` / `bgm` コマンドの引数（音量 / フェード / ループ / pitch / 停止）詳細は実装時に詰める。
- voice の variant 選択（anti-repeat の乱数）は提示層であって物語状態ではないため、将来追加しても
  [決定性コントラクト](/design/decisions/determinism-contract.md) / replay の記録対象外（SE/BGM も同様）。

# 検討した代替案

- **voice_id を `say` に常設**: 全シナリオ依存の中核コマンドに触れる。別コマンド方式の方が非破壊なので不採用。
- **音声系すべて v1 除外**: SE/BGM は基本演出かつ素材ありのため採用する。
