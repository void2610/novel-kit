---
type: Design
title: novel-kit アーキテクチャ
description: 6 アセンブリ構成（+ opt-in Samples~ / Tests）・VitalRouter コマンドバス・INovelView 抽象・MRuby 進行モデル・game 配線。
tags: [architecture, asmdef, vitalrouter, vcontainer, mruby]
timestamp: 2026-06-14T23:59:00Z
---

# アセンブリ分割

6 アセンブリ（+ `Novel.Samples` / `Novel.Tests.EditMode`）+ game 側配線層に分割する。CLAUDE.md の DI 規約に従い、MonoBehaviour は
`Novel.View` のみに置き、`Novel.Runtime` は純 C# とする。

| asmdef | 依存 | 内容 |
|---|---|---|
| `Novel.Commands` | VitalRouter, MRubyCS.Serializer のみ | 全 `[MRubyObject] readonly partial record struct : ICommand`。`IsExternalInit` ポリフィルを内包。publisher/receiver が型だけ共有し疎結合（apocalyptic-apartment-hunting の `Gameplay.Commands` 方式） |
| `Novel.Runtime` | UniTask, VitalRouter, MRubyCS, Novel.Commands | 純 C# コア。シナリオランナー / `IScenarioSource` / `INovelView` / `[Routes] NovelCommandHandler` / プリアンブルローダ / `ParseSymbol<T>` / タイプライタエンジン / `IStateStore`（フラグ/変数/既読）/ バックログ / `INovelPlaybackSettings` + Default |
| `Novel.View` | Novel.Runtime, (任意) LitMotion 等 | 任意の MonoBehaviour 参考 View（メッセージ窓・吹き出し・ナレ・立ち絵・背景・選択 UI）+ アニメツールキット。game は無視して自前 `INovelView` を供給可 |
| `Novel.Editor` | Novel.Runtime, UnityEditor | カタログ/検証インスペクタ。`.rb`→`.mrb` の ScriptedImporter は **mrubycs-compiler パッケージが提供**するため再実装せず、それに乗る |
| `Novel.VContainer` | Novel.Runtime, VContainer, UniTask | 任意の DI 統合層（純 `Novel.Runtime` 依存・View/Resources 非依存）。`RegisterNovelKitCore()` でコア実装（runner / no-op 既定ファセット / silent な NullErrorHandler）を一括登録。game が自前 View / 独自ローダを供給する場合に使う |
| `Novel.View.VContainer` | Novel.Runtime, Novel.View, Novel.VContainer, VContainer | 箱出し用 DI 統合層。`RegisterNovelKit()` = コア（`RegisterNovelKitCore()`）+ Resources ローダ + dev 警告ファセット + `DebugNovelErrorHandler`（Ruby backtrace をログ） |

> 範囲の根拠は [ライブラリ範囲の ADR](/design/decisions/library-scope.md) を参照。

# 進行モデル（MRuby Fiber サスペンション）

全 MRuby プロジェクトで実証済みの共通機構を採用する。

```
.rb シナリオ → cmd 発行 → VitalRouter Router → [Routes] NovelCommandHandler
  → async On(SayCommand, ct): NovelLine 構築（既読/タグ/状態反映, backlog 追記）
     → await INovelView.ShowMessageAsync(line, ct)   ← この await が Ruby Fiber をサスペンド
  → 入力待ち（クリック/Enter/Auto 遅延）→ 解決 → 次 cmd
```

`async UniTask On(cmd, ct)` ハンドラを VitalRouter が `PublishAsync` で待機することで、
Ruby の Fiber が 1 行ごとにサスペンドし、手書きの状態機械なしに「表示 → 待ち → 次」の
線形進行が成立する。これを進行モデルの基盤とする。

ただしこの前進専用モデルは save-anywhere / ロールバックと構造的に衝突する。本ライブラリはこれを割り切り、
セーブ/復元を `PlayAsync` 境界のチェックポイントに限定する（save-anywhere は持たない）。
[実行モデルの ADR](/design/decisions/execution-model.md)（前進専用 + チェックポイント割り切り）を参照。

# INovelView 抽象（最重要の境界）

ハンドラは具体 UI でなく `INovelView` のみに依存する。

```csharp
public interface INovelView {
    UniTask ShowMessageAsync(NovelLine line, CancellationToken ct);
    UniTask<int> ShowChoicesAsync(IReadOnlyList<string> options, CancellationToken ct);
    // 任意ファセット: IPortraitView / IBackgroundView / IAudioChannel を別途解決
}
```

game は自前 UI を実装して供給でき、テストは fake view で進行（say/choice 待ち・auto/skip）を
ヘッドレス検証できる（color-recollection が実証済み）。

# 世界エフェクト（game への脱出経路）

会話の外側（カメラ・画面・gameplay）へ作用するエフェクトは、game が任意で供給する `IWorldEffectSink`
への明示ブリッジで送る。既定はブリッジ無し（VitalRouter を gameplay に使わない game でもノベルは動く）。

```csharp
public interface IWorldEffectSink {
    UniTask DispatchAsync(IWorldEffect effect, CancellationToken ct);  // async（UniTask 返し）
}
```

await 意味論は進行モデルと同一機構で統一する。**ハンドラが await するか否かで blocking/non-blocking が決まる**:
非ブロッキング（`shake`/`flash`）は sink が即完了タスクを返し会話は止まらず、ブロッキング（`fade_out(2.0)`/
`blackout`）は sink が完了時に解決するタスクを返し Fiber がサスペンドして次行が待つ。DSL に blocking フラグは
持たず、per-call の `wait:` 上書きも v1 では持たない。[エフェクトの await 意味論](/design/decisions/effect-await.md)。

# 実行結果とエラー処理

- runner の表面は `UniTask<NovelResult> PlayAsync(scenarioKey, ct)`。`NovelResult` は完了状態を表し、
  **completed / cancelled / faulted** を持つ。分岐に必要な outcome は `IStateStore` に残るので game は完了後に
  状態を読む（[フロー/シーケンサの境界](/design/decisions/flow-boundary.md)）。
- MRuby 実行時例外は try/catch で捕捉し backtrace を surface する。リリースではフェイルセーフとして
  `NovelResult.Faulted` を返し、game が注入する `INovelErrorHandler` に委譲する。サンドボックスは v1 では持たない
  （一次コンテンツ前提）。[MRuby エラー処理・サンドボックス](/design/decisions/error-handling.md)。

# コマンドバスと語彙

- ランナーが `DefineVitalRouter(config => config.AddCommand<SayCommand>("say"), ...)` で
  Ruby コマンド名と C# コマンド record struct を束ねる。
- 語彙はリッチ統一語彙を常設し、未配線コマンドは no-op デフォルトハンドラで握りつぶす
  （`.rb` が常に動く保証）。詳細は [DSL 語彙の ADR](/design/decisions/dsl-vocabulary.md)。
- enum は常に snake_case 文字列で受け、単一の `ParseSymbol<T>` ヘルパーで解決する
  （MRubyCS.Serializer の `EnumAsStringFormatter` に Deserialize が無い制約の回避を一本化）。
- 著者向けには一度ロードされるプリアンブル `preamble.rb` が
  `say/narration/choose/flag/portrait/bg/still/se/bgm/wait` 等の糖衣を提供し、game が拡張可能。
  voice は v1 除外（将来は別コマンド + 糖衣）。[音声スコープ](/design/decisions/audio-scope.md)。

# ルーター所有権

**ノベル専用 Router を `NovelLifetimeScope` に container 登録**し、`NovelCommandHandler` を
DI 市民としてその Router にマップする（静的 `Router.Default` でも game の gameplay Router でもない）。
container 所有 / runner 私有 `new Router()` / 静的 `Router.Default` の 3 モデル混在を本方針で一本化する。
**`INovelRouterProvider` のような抽象は入れない**（既定を 1 つに決め切り、必要が出たら後から足す）。
[ルーター所有権の ADR](/design/decisions/router-ownership.md) で確定。

世界エフェクト（カメラ振動・ブラックアウト等）は静的 global ではなく、game が任意で供給する
`IWorldEffectSink` への**明示ブリッジ**で脱出させる（既定はブリッジ無し）。詳細は下記「世界エフェクト」を参照。

# 状態と永続化

- フラグ / 変数 / 既読を単一の `IStateStore` に統一する。[状態モデルの ADR](/design/decisions/state-model.md)。
- 永続の対象は `IStateStore` のみ。snapshot の出し入れは runner の `CaptureState()`/`RestoreState()`、直列化は
  `NovelSaveData`（クラス）/ `NovelSaveSerializer`（文字列）。実際の保存は game 所有（`ISaveStore` は撤去）。
- **セーブ境界は `PlayAsync` の間**（シナリオ完了の狭間）。シナリオ途中での保存は v1 対象外。提示状態
  （表示中の窓・進行中タイプライタ等）は非シリアライズ。[セーブのスナップショット粒度](/design/decisions/save-snapshot.md)。
- 入力履歴（選択 index・フラグ列）の記録は持たない（実行モデルが前進専用 + チェックポイント割り切りへ格下げされたため）。

# シナリオソース

`IScenarioSource` が論理キー → `.mrb` バイトコード（`byte[]`、sub-asset の `TextAsset`）を解決する。
`Irep` パースは MRubyState 依存のため runner 側で行う。

実装は flat な Resources 方式（`ResourcesScenarioSource`）の 1 実装。空キーで `LoadAll('Scenarios/')` が
root 配下を総なめする空 id フットガンは内部でガードする（空キーは即 `null`）。`{key}/{Common}` 構造化 fallback 方式は
当面持たない（必要が出たら追加。game は `IScenarioSource` を自前実装で差し替え可）。

# テキスト提示

タイプライタは `maxVisibleCharacters` 直制御方式から、行内タグ言語の字句解析ベースへ再設計する
（Reveal 中の pause / 速度変更 / 部分スタイルに対応）。[インラインタグの ADR](/design/decisions/inline-tags.md)。

# game 側の配線（LifetimeScope）

game は `NovelLifetimeScope` で以下を登録する。

- `INovelView` 実装（`RegisterComponentInHierarchy` または `RegisterInstance`）
- `IScenarioSource` / `IStateStore` / `INovelPlaybackSettings`（セーブは runner の `CaptureState`/`RestoreState` + 自前 serde）
- 任意の `ICharacterCatalog` / `IAudioChannel` / `IWorldEffectSink` / `INovelErrorHandler` / `ITextResolver`
- novel 専用 Router（`RegisterVitalRouter`。`Router.Default` でも gameplay Router でもない）
- `NovelScenarioRunner` をエントリポイントとして登録

起動は `runner.PlayAsync(scenarioKey, ct)` を game の任意の箇所から呼ぶ。chapter/phase/
auto-advance/retry といったフロー制御は **game 側に残す**（ライブラリは `PlayAsync` と完了結果
のみ公開）。これにより color-recollection の `KeywordGate` 的なゲーム固有フローをライブラリ外に保つ。
