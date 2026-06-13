---
type: Design
title: novel-kit アーキテクチャ
description: 4 アセンブリ構成・VitalRouter コマンドバス・INovelView 抽象・MRuby 進行モデル・game 配線。
tags: [architecture, asmdef, vitalrouter, vcontainer, mruby]
timestamp: 2026-06-13T00:00:00Z
---

# アセンブリ分割

4 アセンブリ + game 側配線層に分割する。CLAUDE.md の DI 規約に従い、MonoBehaviour は
`Novel.View` のみに置き、`Novel.Runtime` は純 C# とする。

| asmdef | 依存 | 内容 |
|---|---|---|
| `Novel.Commands` | VitalRouter, MRubyCS.Serializer のみ | 全 `[MRubyObject] readonly partial record struct : ICommand`。`IsExternalInit` ポリフィルを内包。publisher/receiver が型だけ共有し疎結合（apocalyptic-apartment-hunting の `Gameplay.Commands` 方式） |
| `Novel.Runtime` | UniTask, VitalRouter, MRubyCS, Novel.Commands | 純 C# コア。シナリオランナー / `IScenarioSource` / `INovelView` / `[Routes] NovelCommandHandler` / プリアンブルローダ / `ParseSymbol<T>` / タイプライタエンジン / `IStateStore`（フラグ/変数/既読）/ バックログ / `INovelPlaybackSettings` + Default |
| `Novel.View` | Novel.Runtime, (任意) LitMotion 等 | 任意の MonoBehaviour 参考 View（メッセージ窓・吹き出し・ナレ・立ち絵・背景・選択 UI）+ アニメツールキット。game は無視して自前 `INovelView` を供給可 |
| `Novel.Editor` | Novel.Runtime, UnityEditor | `.rb` ScriptedImporter（`.mrb` sub-asset 生成）+ カタログ/検証インスペクタ |

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

ただしこの前進専用モデルは save-anywhere / ロールバックと構造的に衝突する。対処は
[実行モデルの ADR](/design/decisions/execution-model.md)（リプレイ前提設計）を参照。

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

# コマンドバスと語彙

- ランナーが `DefineVitalRouter(config => config.AddCommand<SayCommand>("say"), ...)` で
  Ruby コマンド名と C# コマンド record struct を束ねる。
- 語彙はリッチ統一語彙を常設し、未配線コマンドは no-op デフォルトハンドラで握りつぶす
  （`.rb` が常に動く保証）。詳細は [DSL 語彙の ADR](/design/decisions/dsl-vocabulary.md)。
- enum は常に snake_case 文字列で受け、単一の `ParseSymbol<T>` ヘルパーで解決する
  （MRubyCS.Serializer の `EnumAsStringFormatter` に Deserialize が無い制約の回避を一本化）。
- 著者向けには一度ロードされるプリアンブル `preamble.rb` が
  `say/narration/choose/flag/portrait/bg/still/se/wait` 等の糖衣を提供し、game が拡張可能。

# ルーター所有権（未決）

container 所有（`RegisterVitalRouter`）/ runner 私有 `new Router()` / 静的 `Router.Default` の
3 モデルが既存プロジェクトで混在。`INovelRouterProvider` で抽象化し container 所有を既定とする
案が有力だが未確定。[残論点](/design/open-questions.md) を参照。

# 状態と永続化

- フラグ / 変数 / 既読を単一の `IStateStore` に統一する。[状態モデルの ADR](/design/decisions/state-model.md)。
- 永続化は game が実装する `ISaveStore` 経由（ライブラリはシリアライズ形式を持たない）。
- リプレイのため、選択 index・フラグ操作などの入力履歴を day 1 から記録する。

# シナリオソース

`IScenarioSource` が論理キー → `Irep`（`.mrb` sub-asset 優先、editor のみソースコンパイル
fallback）を解決する。flat な Resources 方式と、構造化 `{key}/{Common}` fallback 方式の
2 実装を用意（`LoadAll('Scenarios/')` の空 id フットガンを内部でガード）。

# テキスト提示

タイプライタは `maxVisibleCharacters` 直制御方式から、行内タグ言語の字句解析ベースへ再設計する
（Reveal 中の pause / 速度変更 / 部分スタイルに対応）。[インラインタグの ADR](/design/decisions/inline-tags.md)。

# game 側の配線（LifetimeScope）

game は `NovelLifetimeScope` で以下を登録する。

- `INovelView` 実装（`RegisterComponentInHierarchy` または `RegisterInstance`）
- `IScenarioSource` / `ISaveStore` / `IStateStore` / `INovelPlaybackSettings`
- 任意の `ICharacterCatalog` / `IAudioChannel`
- novel 用 Router（`RegisterVitalRouter` または provider）
- `NovelScenarioRunner` をエントリポイントとして登録

起動は `runner.PlayAsync(scenarioKey, ct)` を game の任意の箇所から呼ぶ。chapter/phase/
auto-advance/retry といったフロー制御は **game 側に残す**（ライブラリは `PlayAsync` と完了結果
のみ公開）。これにより color-recollection の `KeywordGate` 的なゲーム固有フローをライブラリ外に保つ。
