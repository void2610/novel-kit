---
type: Decision
title: プロジェクトリファレンス — キー列挙はチャンネル契約に統合・編集モードへは明示登録
description: ライター向けに「使える名前と構図」を一覧するエディタウィンドウを追加する。列挙の契約は IAudioChannel / IPortraitChannel 自身に default 実装付きで統合し、編集モードへのインスタンス供給は InitializeOnLoad の明示登録で行う。参考実装として ScriptableAudioCatalog + NovelAudioPlayer を追加する。
tags: [decision, editor, tooling, audio, portrait, layout, catalog, writer]
timestamp: 2026-08-06T05:01:56Z
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

## 3. 編集モードへの供給は InitializeOnLoad の明示登録

エディタウィンドウは編集モードで動くが、チャンネルは実行時 DI でしか実体化しない。
このギャップは **game（および novel-kit 参考実装）が editor アセンブリで明示登録**して埋める。

```csharp
#if UNITY_EDITOR
[InitializeOnLoad]
static class MyNovelEditorHooks
{
    static MyNovelEditorHooks() =>
        NovelProjectReference.RegisterAudio(() => new MyAudioChannel(/* editor-safe に構築 */));
        // RegisterPortrait(...) も同様
}
#endif
```

- ウィンドウは登録済みファクトリを呼んで列挙するだけ。**リフレクション走査・アセット形式の仮定
  （SO/MonoBehaviour/plain class）は一切持たない**。データがどこにあるか（SO・enum・JSON・シーン上の
  コンポーネント）はファクトリを書く game の自由。
- 未登録セクションはウィンドウに「未登録」と表示する。これがそのまま配線状況の簡易可視化を兼ねる。

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
- 明示登録は決定的で、発見の失敗が「未登録」として見える。型走査より単純で説明可能。
- default 実装により後方互換が保たれ、既存 6 プロジェクトのチャンネル実装に影響しない。

# 帰結

- `IAudioChannel` / `IPortraitChannel` にメンバー追加（default 実装付き・非破壊）。
  `AudioKeyInfo` / `StageLayoutInfo` を追加。
- `Novel.Editor` に登録レジストリ（`NovelProjectReference`）とウィンドウを追加。
- game 側の導入作業は「editor アセンブリに InitializeOnLoad の登録 1 クラス」+
  「チャンネルの列挙オーバーライド」。
- 列挙契約は将来の Validate Scenarios 突き合わせ（シナリオ中の未定義キー検出）の
  正解データをそのまま兼ねる（次フェーズ候補、本 ADR のスコープ外）。
- シーン上のインスタンス有無に依存しない（登録ファクトリが editor-safe に構築する責務を持つ）。

# 検討した代替案

- **手書きマニフェスト SO（宣言一元化）**: 自動で取れない情報を game が SO に転記する案。
  自前音響を持つ game では既存カタログとの二重管理になり陳腐化するため不採用。
- **別 interface（IAudioKeyProvider）+ 型走査で発見**: TypeCache で実装型を列挙し、
  SO はアセット検索・MonoBehaviour はシーン/プレハブ検索・plain class は引数なし生成、と
  入手経路が型ごとに分岐する。暗黙的で説明困難、シーンを開いているかに挙動が依存する、
  発見失敗が沈黙する、と複雑さの割に堅牢でないため不採用。チャンネル統合 + 明示登録が
  同じ範囲をより単純に覆う。
- **Markdown 書き出し先行**: Unity を開かないライターへの配布には有効だが、ユーザー判断で
  最初からエディタウィンドウに一本化。エクスポートは必要になれば後付けできる。
