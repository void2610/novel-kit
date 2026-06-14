---
type: Decision
title: 行内インラインタグ言語を v1 実装
description: maxVisibleCharacters 直制御から字句解析ベースのタイプライタへ再設計。記法は TMP `<...>` 単一・ライブラリ所有 lexer・非タグはエスケープ。
tags: [decision, text, typewriter, tags]
timestamp: 2026-06-15T00:00:00Z
status: 確定
---

# 状況

既存実装のテキスト効果は行単位変換のみ（ルビ・キーワード markup・`ProductionSubtitleStyle` enum）。
`say()` 内で途中ポーズ・速度変更・部分着色・shake ができない。これは VN タイプライタの table-stakes
（Ren'Py の `{w}{p}{fast}`、Naninovel のインラインタグ）であり、全プロジェクトに欠落している。
現行の color-recollection は `maxVisibleCharacters` アキュムレータ方式で、途中ポーズ/再スタイルができない。

# 決定

**行内インラインタグ言語を v1 で実装**し、タイプライタを `maxVisibleCharacters` 直制御から
**字句解析（パーサ → トークン列 → Reveal 中に pause/restyle）** ベースへ再設計する。

## 記法は TMP `<...>` 単一

タグはすべて TMP リッチテキストと同じ `<...>` 記法に統一する（`{...}`/`[...]` は使わない）。

```ruby
say "待って…<w=0.5>本気<shake>なの</shake>？　<color=#f88>嘘でしょ</color>　<link=\"kw_truth\">真実</link>"
```

## ライブラリ所有の lexer（安全性の核）

`Novel.Runtime` の lexer が文字列全体を**既知のタグ集合**に対して走査する。生テキストを TMP に素通し
**しない**（素通しは player 名や変数展開中の `<` でレイアウト崩壊/注入を招くため）。

- **TMP スタイル**（`<color>` `<size>` `<b>` `<i>` …）→ 認識して TMP へ（基本そのまま）。
- **自前制御**（`<w=N>` `<speed=x>`…`</speed>` `<shake>`…`</shake>` `<wave>`…）→ 制御トークンとして抽出し、
  TMP に渡す文字列からは除去。Reveal 中に pause/速度変更/頂点アニメを駆動。
- **意味タグ**: `<ruby=よみ>漢字</ruby>` → TMP の `<voffset>`/`<size>`/`<space>` に展開。
  クリック可能キーワードは TMP ネイティブ `<link="id">…</link>`（CR のヒットテスト方式）をそのまま使う。
- **それ以外の素テキスト**（認識タグにならない `<` を含む）→ `< > &` を**エスケープ**してから TMP へ。
  これにより `player_name="A<B"` のような動的入力でも壊れない。
- タグそっくりのリテラル文字列を表示したい稀ケース用に `<noparse>` 相当のエスケープタグを 1 つ用意する。

パースは Novel.Runtime（`say` の `Text` は生マークアップを載せるだけ。Ruby 文字列補間では表さない）。

## v1 タグセット（実装時に最終確定）

| タグ | 意味 |
|---|---|
| `<w=N>` | N 秒待つ |
| `<p>` | クリック待ち（行内ポーズ・続けて同じ行を表示） |
| `<speed=2x>`…`</speed>` | 区間の表示速度変更 |
| `<fast>` | 以降を即時表示 |
| `<shake>`…`</shake>` / `<wave>`…`</wave>` | 頂点アニメ |
| `<ruby=よみ>漢字</ruby>` | インラインルビ → TMP タグへ展開 |
| `<color>`/`<size>`/`<b>`/… | TMP スタイル（素通し） |
| `<link="id">…</link>` | クリック可能キーワード（TMP ネイティブ） |
| `<noparse>`…`</noparse>` | リテラル表示（エスケープ） |

# 理由

- 行内制御は VN として必須の表現力で、後付けはエンジン契約の作り直しになるため最初から入れる。
- 字句解析式なら TMP リッチテキストタグと独自タイミングタグを分離して扱え、可変長の途中挿入にも対応。

# 帰結

- タイプライタエンジンの契約は「トークン列を逐次 Reveal し、タイミング/スタイルトークンで
  pause/restyle する」形になる。`Novel.Runtime` の純 C# 状態機械（void-red `TextProgressController` 形）
  + TMP 技法（CR の deltaTime アキュムレータ、タグを文字数にカウントしない扱い）を統合する。
- タグ構文は本 ADR で確定（TMP `<...>` 単一・ライブラリ所有 lexer・非タグはエスケープ・パースは Novel.Runtime）。
- プレーンテキスト抽出（バックログ表示・既読ハッシュ FNV）は全タグ除去後の文字列で行う（CR の `ToPlainText` を
  本タグ体系に対応）。
- ルビ/clickable markup は行内タグ体系に取り込みつつ、JP/ゲーム固有部分（辞書ルビ等）は任意モジュール化する。

## 実装で確定（2026-06-14, 実装レビュー後）

帰結が示した「逐次 Reveal の純 C# 状態機械を `Novel.Runtime` に置く」を `TextRevealEngine`（+ フレーム時間抽象
`IFrameClock`）として実装。速度蓄積・`<w>`/`<p>`/`<fast>`/`<speed>` 解釈・shake/wave 区間算出・auto/skip 進行を
Runtime に集約し、`NovelMessageView` は可視文字数の反映と頂点演出だけを担う薄い TMP アダプタになった（自前 View は
進行ロジックを再実装せずエンジンを駆動するだけ）。`<speed>`/`<shake>`/`<wave>` の区間はスタックで入れ子に対応し、
`<w>` 待機中のクリックでも即時全表示が効く。`IFrameClock` を fake にすればヘッドレス検証可能。
当初は状態機械が View に流出していた（[実装レビュー](/design/implementation-review.md) `NK-TYPEWRITER-IN-VIEW`/
`NK-SPAN-NEST`/`NK-WAIT-CLICK`）のを解消。

## 実装で確定（2026-06-15, ルビ展開を実装）

本 ADR が表（55 行）で約束した `<ruby=よみ>漢字</ruby>` → TMP 展開を実装した。初期実装は lexer が ruby を `Ignored`
（読み未展開で親文字のみ素通し）に格下げしていたのを、`RubyPush`（Payload=よみ）/`RubyPop` トークンへ昇格。
`<ruby=かんじ>漢字</ruby>` は `RubyPush("かんじ") / Text("漢字") / RubyPop` に分解される。座標トリック展開
（`<space>`/`<voffset>`/`<size>` でよみを親文字の上に重ねる）は TMP 固有なので `Novel.View` の `RubyMarkup.BuildOverlay`
に置き、lexer は Runtime に置く（self-View は自前展開可）。**よみは TMP 上も文字数を持つため `TextRevealEngine.Build` が
可視数に算入**し（`RubyPush` で `よみ.Length` を加算）、View 展開後の `characterCount` と総数を一致させて Reveal を
ずらさない。`ToPlainText`（既読ハッシュ・バックログ平文）はよみを除き親文字だけを残す（ふりがなで既読が割れない）。
辞書ルビ・`:first`（初出のみ表示）等の JP/ゲーム固有部分は引き続き game の任意モジュール（color-recollection の
`RubyDictionary` が `<ruby>` タグ挿入側を担い、本ライブラリは展開を担う分業）。

# 検討した代替案

- **行単位スタイル属性のみ**（`say "...", style: :impact`）: 最小だが行途中の制御不可・後付け困難。不採用。
- **既存 markup 踏襲のみ**（whole-line のルビ/キーワード）: インライン制御なし。不採用。
- **自前タグを別デリミタ `{...}`/`[...]` にする**: 一度は安全策として検討したが、ライブラリが lexer を所有して
  非タグをエスケープする時点でデリミタは安全性に効かず、`<...>` 単一の方が TMP スタイルを素通しでき著者の
  既存知識も流用できるため不採用（デリミタの差は「タグそっくりのリテラル表示」の稀ケースのみで、`<noparse>` で対応）。
