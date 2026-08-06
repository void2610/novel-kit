---
type: Decision
title: プロジェクトリファレンス — キー列挙はチャンネル契約に統合・実体は DI ビルド時にキャプチャ
description: ライター向けに「使える名前と構図」を一覧するエディタウィンドウを追加する。列挙の契約は IAudioChannel / IPortraitChannel 自身に default 実装付きで統合し、実行時にしか実体がない情報は RegisterNovelKitCore が DI ビルド時にキャプチャしてエディタ側キャッシュへ渡す（game 側の追加記述ゼロ）。参考実装として ScriptableAudioCatalog + NovelAudioPlayer を追加する。
tags: [decision, editor, tooling, audio, portrait, layout, catalog, writer]
timestamp: 2026-08-06T05:20:00Z
status: 確定
---

# 状況

シナリオライターズガイド（`Docs/scenario/`、2026-08-06 整備）を書いた結果、ガイドが
「プロジェクトの資料で確認」「エンジニアに確認」へ逃げている箇所が 13 箇所あることが分かった。
内訳は (1) 使える名前の一覧（キャラ id・立ち絵/背景/一枚絵/補足画像キー・SE/BGM キー）、
(2) 構図（レイアウト）の一覧、(3) 独自命令、(4) 配線状況。

情報源はすべて Unity アセット・C# コード・DI 登録に散っており非プログラマには読めない。
かつ novel-kit は未配線・キー間違いを no-op で流す設計（[DSL 語彙](/design/decisions/dsl-vocabulary.md)）
のため、書き間違いが沈黙し、一覧なしでは通し確認頼みになる。手書き資料は必ず陳腐化する。

ユーザーとの議論（2026-08-06）で、主目的を **名前と構図の確認**に絞り、
最初からエディタウィンドウとして実装する（Markdown 書き出しはしない）ことで合意した。

# 決定

## 1. エディタウィンドウでの一覧（v1 スコープ = 名前 + 構図）

`Novel.Editor` にプロジェクトリファレンスウィンドウ（メニュー名は実装時確定。例: Novel > Project Reference）
を追加し、以下を一覧する。

| セクション | 情報源 |
|---|---|
| キャラ（id・表示名・既定立ち絵） | `ScriptableCharacterCatalog` アセット検索（既存資産） |
| 画像キー（立ち絵/背景/一枚絵/補足画像） | Resources 配下のスプライトスキャン（キー = Resources 相対パスの規約に基づく） |
| 構図（レイアウト id・スロット数） | `IPortraitChannel.EnumerateLayouts()`（下記 2） |
| SE / BGM キー | `IAudioChannel.EnumerateKeys()`（下記 2） |

Markdown 等へのエクスポートは v1 では持たない。独自命令一覧・実行時配線診断はスコープ外。

## 2. 列挙の契約はチャンネル自身に統合する

「キーを解決して再生する実装が、キーの一覧も知っている」のは自明なので、
別 interface（provider）を新設せず **既存ファセットに列挙メンバーを統合**する。

```csharp
// Novel.Runtime
public interface IAudioChannel
{
    // …既存の再生 API…
    IEnumerable<AudioKeyInfo> EnumerateKeys() => Array.Empty<AudioKeyInfo>();   // default 実装
}

// Novel.Assets
public interface IPortraitChannel
{
    // …既存 API…
    IEnumerable<StageLayoutInfo> EnumerateLayouts() => StageLayoutInfo.Defaults; // 標準 5 構図
}
```

- default interface method により **既存実装（WarningNullFacets・各ゲームのチャンネル）は無改修**で
  コンパイルが通る。列挙に参加したい実装だけオーバーライドする。
- `AudioKeyInfo` は key / 種別（BGM/SE）/ ライター向けメモ（任意）、`StageLayoutInfo` は
  layout id / スロット数 / メモ（任意）を持つ軽量構造体。

## 3. 実行時にしか実体がない情報は DI ビルド時にキャプチャする

エディタウィンドウは編集モードで動くが、チャンネルは実行時 DI でしか実体化しない。
このギャップは編集モード側で埋めない。**実際にコンテナが組み上がる瞬間に novel-kit 自身が吸い上げる**。

`RegisterNovelKitCore()`（game が既に呼んでいる）にエディタ実行時のみ有効な
`RegisterBuildCallback` を仕込み、構築済みコンテナから `IAudioChannel` / `IPortraitChannel` を
解決して列挙結果をエディタ側キャッシュ（`Library/` 配下・ドメインリロード/再起動を跨いで保持）へ書き出す。
ウィンドウはキャッシュを取得時刻つきで表示する。

- **game 側の追加記述はゼロ。** チャンネル実装 + 列挙オーバーライド + いつもの DI 登録だけで
  ウィンドウに載る。チャンネルの実体が plain class か MonoBehaviour か等の入手問題は、
  DI が解決できている時点で存在しない。
- **忠実度が最高。** 後勝ち差し替え・スコープ構成まで含めた「実際に鳴らす配線」から取るため、
  編集モードでの再構築のような本番との乖離が原理的にない。
- トレードオフとしてデータは「最後に再生した時点」のもの。未キャプチャ時はウィンドウが
  「一度再生してください」と案内する。ただしアセットとして静的に読めるもの
  （キャラカタログ・Resources の画像キー・参考 ScriptableAudioCatalog）は再生不要でライブ表示し、
  キャッシュ頼みになるのは実行時にしか実体がない部分（自前音響等）に限る。
- キャプチャは try/catch で保護し、失敗しても game の起動を妨げない（警告ログのみ）。

## 4. 参考実装: ScriptableAudioCatalog + NovelAudioPlayer

音は現状 interface のみで参考実装が無く、キーの実体がライブラリのどこにも無い。
既存パターン（参考 View / SO カタログ / 後勝ち差し替え / 未配線警告）に揃え、`Novel.View` に追加する。

- `ScriptableAudioCatalog`（SO）: キー → AudioClip（BGM/SE 区分・メモ付き）。
- `NovelAudioPlayer`: `IAudioChannel` の参考実装。カタログでキー解決して AudioSource で再生。
  v1 は最小限 — BGM 再生/切替/停止、SE ワンショット、`se_loop` 対応。クロスフェード・
  ミキサー連携・音量設定連動は持たない（必要な game が差し替える）。
- 参考実装も 3 の登録経路で自分を登録する、ただの一利用者。自前音響の game はこれを使わず、
  自分のチャンネルに `EnumerateKeys()` をオーバーライドすればよい。

# 理由

- 列挙をチャンネル契約に統合すると、契約が 1 つで済み、「鳴らせるのに一覧に出ない」という
  実装とカタログの乖離が構造的に起きない。
- DI ビルド時キャプチャは、game が既に書いている配線（interface 実装 + DI 登録）以外の
  記述を一切要求しない。配線の知識を二重に書かせる案（マニフェスト・editor フック）は
  すべて同型の陳腐化リスクを持つため退けた。
- default 実装により後方互換が保たれ、既存 6 プロジェクトのチャンネル実装に影響しない。

# 帰結

- `IAudioChannel` / `IPortraitChannel` にメンバー追加（default 実装付き・非破壊）。
  `AudioKeyInfo` / `StageLayoutInfo` を追加。
- `Novel.VContainer` の `RegisterNovelKitCore()` にエディタ限定のキャプチャコールバックを追加。
  受け口（キャプチャハブ）は `Novel.Runtime` に `#if UNITY_EDITOR` で置き、
  `Novel.Editor` が購読して `Library/` 配下へ永続化 + ウィンドウ表示する。
- game 側の導入作業は「チャンネルの列挙オーバーライド」のみ。
- 列挙契約は将来の Validate Scenarios 突き合わせ（シナリオ中の未定義キー検出）の
  正解データをそのまま兼ねる（次フェーズ候補、本 ADR のスコープ外）。
- `EnumerateKeys()` / `EnumerateLayouts()` は起動負荷を避けるため軽量であること
  （キー列挙のみ。アセットの実ロードを伴わないこと）を契約ドキュメントに明記する。

# 検討した代替案

- **手書きマニフェスト SO（宣言一元化）**: 自動で取れない情報を game が SO に転記する案。
  自前音響を持つ game では既存カタログとの二重管理になり陳腐化するため不採用。
- **別 interface（IAudioKeyProvider）+ 型走査で発見**: TypeCache で実装型を列挙し、
  SO はアセット検索・MonoBehaviour はシーン/プレハブ検索・plain class は引数なし生成、と
  入手経路が型ごとに分岐する。暗黙的で説明困難、シーンを開いているかに挙動が依存する、
  発見失敗が沈黙する、と複雑さの割に堅牢でないため不採用。
- **InitializeOnLoad の明示登録（editor フックでファクトリ登録）**: 発見は決定的になるが、
  game が DI に書いた配線と同じ知識を editor 用にもう一度（しかも MonoBehaviour では
  入手経路の選択込みで）書かせる二重記述になるため不採用。DI ビルド時キャプチャが
  同じ情報を追加記述ゼロで得る。
- **Markdown 書き出し先行**: Unity を開かないライターへの配布には有効だが、ユーザー判断で
  最初からエディタウィンドウに一本化。エクスポートは必要になれば後付けできる。
