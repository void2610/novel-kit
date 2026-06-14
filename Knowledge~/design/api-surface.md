---
type: Design
title: 公開 API 表面（凍結）
description: 確定 15 ADR を統合した novel-kit の公開表面。ランナー / コマンド / View 抽象 / game 供給サービスのシグネチャを 1 箇所に集約する。
tags: [api, surface, freeze, contract, runner, view]
timestamp: 2026-06-14T19:35:00Z
status: 確定
---

# 位置づけ

確定済み 15 [ADR](/design/decisions/index.md) が個別に定めた型・契約を、**実装の出発点となる公開表面**として
1 箇所に集約する。各シグネチャの根拠 ADR を併記する。`novel-kit` はゲーム内ノベルパートのプリミティブであり
（[概要](/design/overview.md) / [フロー境界](/design/decisions/flow-boundary.md)）、進行・章/フェーズ・分岐は game 側に残す。

**凍結の粒度**: ここで確定するのは型と契約の *形* と *責務境界*。メンバ単位の最終 C# シグネチャ
（`se`/`bgm` の引数詳細・v1 インラインタグセットの細部等）は実装時に最終確定する旨を各所に明記する。
アセンブリ分割は [アーキテクチャ](/design/architecture.md) を参照（本書は表面のみ）。

# 1. ランナー表面（コア）

game が触れる唯一のエントリポイント。

```csharp
public interface INovelScenarioRunner {
    // 1 シナリオを完了まで再生し結果を返す。これがコア表面の全て。
    UniTask<NovelResult> PlayAsync(string scenarioKey, CancellationToken ct);
}

// 完了状態のみを表す。分岐に必要な outcome は IStateStore に残る。
public enum NovelResult {
    Completed,   // 正常完了
    Cancelled,   // ct によるキャンセル
    Faulted,     // MRuby 実行時例外でフェイルセーフ終了（INovelErrorHandler に委譲）
}
```

- 線形シーケンサ / シナリオ間 `goto` は持たない。次シナリオの選択は game が `PlayAsync` 完了後に
  `IStateStore` を読んで決める。[フロー境界](/design/decisions/flow-boundary.md)。
- セーブ境界は `PlayAsync` の狭間。シナリオ途中保存は v1 対象外。[セーブ粒度](/design/decisions/save-snapshot.md)。
- **再入契約**: シナリオ実行中の例外は握って `Faulted`/`Cancelled` に畳む（フェイルセーフ）。ただし
  **再生中（前の `PlayAsync` 完了前）の再入呼び出しは `InvalidOperationException` を投げる**（単一 `MRubyState`
  共有のための fail-fast。API 誤用＝配線バグの検出であり、コンテンツ障害の `Faulted` とは別カテゴリ）。
  game は同一 runner の再生を直列化する。

# 2. コマンド層（`Novel.Commands`）

行コマンドは `say` プリミティブ 1 つ。キャラ名コマンド / `narration` は `preamble.rb` の糖衣が
`say` に落とすだけで C# コマンドを増やさない。[コマンド名規約と say スキーマ](/design/decisions/command-schema.md)。

```csharp
[MRubyObject]
readonly partial record struct SayCommand : ICommand {
    public string  SpeakerId { get; init; }   // "" / null = ナレーション
    public string? DisplayAs { get; init; }    // 任意: 表示名の上書き（名前リビール）
    public string  Text      { get; init; }   // インラインタグ生テキスト（Runtime で字句解析）
}
```

解決規則: ① `SpeakerId` 空 → ナレーション ② カタログにあり → 表示名/立ち絵/既定ボイスを使用（`DisplayAs` で
表示名のみ上書き）③ カタログに無い → id 文字列をそのまま表示名（その場話者）。

- voice フィールドは持たない（v1 除外・将来は独立 `voice` コマンド + 糖衣）。[音声スコープ](/design/decisions/audio-scope.md)。
- 語彙はリッチ統一語彙を常設し、未配線コマンドは no-op デフォルトハンドラで握りつぶす。[DSL 語彙](/design/decisions/dsl-vocabulary.md)。
- versioning 機構は持たない（`.rb` 正・再生成で追従）。[コマンドスキーマ versioning](/design/decisions/command-versioning.md)。
- preamble 糖衣: `say/narration/choose/flag/portrait/bg/still/se/bgm/wait` 等。各コマンドの引数詳細は実装時確定。

# 3. View 抽象（game が実装、またはライブラリ参考 View を使用）

ハンドラは具体 UI でなく抽象のみに依存する。[アーキテクチャ](/design/architecture.md) の最重要境界。

```csharp
public interface INovelView {
    UniTask      ShowMessageAsync(NovelLine line, CancellationToken ct);
    UniTask<int> ShowChoicesAsync(IReadOnlyList<string> options, CancellationToken ct);
}

// 任意ファセット（game が必要に応じて実装・別途解決）
public interface IPortraitView   { /* 単一スロット 1 枚差し替え。多層合成/複数配置は v1 無し */ }  // character-model
public interface IBackgroundView { /* 背景差し替え */ }
public interface IAudioChannel   { /* se / bgm。引数詳細は実装時確定 */ }                          // audio-scope
```

- `NovelLine` は既読/タグ/状態反映済みの提示単位（Runtime が `SayCommand` から構築）。
- 立ち絵は単一スプライト差し替え（表情変更も全体指定）。[キャラクターモデル](/design/decisions/character-model.md)。
- テストは fake view で進行（say/choice 待ち・auto/skip）をヘッドレス検証できる。

# 4. game 供給サービス（`NovelLifetimeScope` に登録）

| サービス | 役割 | 根拠 ADR |
|---|---|---|
| `IScenarioSource` | 論理キー → `.mrb` バイトコード（`byte[]`）。`Irep` パースは MRubyState 依存のため runner 側 | [アーキテクチャ](/design/architecture.md) |
| `IStateStore` | フラグ / 変数 / 既読を単一統合。**runtime 内部実装（`MRubyStateStore`）が既定**で game 供給不要 | [状態モデル](/design/decisions/state-model.md) |
| `ISaveStore` | 永続化（ライブラリはシリアライズ形式を持たない。対象は `IStateStore` のみ） | [セーブ粒度](/design/decisions/save-snapshot.md) |
| `INovelPlaybackSettings` | auto/skip 速度等の再生設定（Default 提供） | [アーキテクチャ](/design/architecture.md) |
| `ICharacterCatalog` | id → 表示名/立ち絵/side/既定ボイス。未登録は id を表示名にフォールバック | [コマンド名規約](/design/decisions/command-schema.md) |
| `IWorldEffectSink` | 世界エフェクトの脱出先（async）。既定はブリッジ無し | [エフェクト await](/design/decisions/effect-await.md) |
| `INovelErrorHandler` | MRuby 実行時例外の委譲先（backtrace surface） | [エラー処理](/design/decisions/error-handling.md) |
| `ITextResolver` | `say` テキスト解決フック（既定は恒等）。多言語は非破壊後付け | [ローカライズ](/design/decisions/localization.md) |

## IStateStore

```csharp
public interface IStateStore {
    // flags / vars / 既読を統合。永続/一時/既読の境界は属性として内部で扱う。
    // choose() はユニークキー自動割当で衝突を防ぐ。unset/clear 経路を持つ。
}
```

## IWorldEffectSink（async・ハンドラ await で統一）

```csharp
public interface IWorldEffectSink {
    UniTask DispatchAsync(IWorldEffect effect, CancellationToken ct);
}
```

- 非ブロッキング（`shake`/`flash`）→ sink が即完了タスクを返す → 会話は止まらない。
- ブロッキング（`fade_out(2.0)`/`blackout`）→ sink が完了時解決タスクを返す → Fiber サスペンド → 次行が待つ。
- DSL に blocking フラグは持たず、per-call `wait:` 上書きも v1 無し。[エフェクト await](/design/decisions/effect-await.md)。

## ITextResolver

```csharp
public interface ITextResolver {
    string Resolve(string raw);   // 既定は恒等変換。将来キー外部化/ロケール解決を差し替え
}
```

# 5. ルーター所有権

ノベル専用 Router を `NovelLifetimeScope` に container 登録し、`NovelCommandHandler`（`[Routes]` 規約）を
DI 市民としてマップする。静的 `Router.Default` でも gameplay Router でもない。`INovelRouterProvider` のような
抽象は入れない。[ルーター所有権](/design/decisions/router-ownership.md)。

# 凍結ステータス

| 項目 | 状態 |
|---|---|
| ランナー表面（`PlayAsync` / `NovelResult`） | ✅ 凍結 |
| `SayCommand` スキーマ・話者解決規則 | ✅ 凍結 |
| `INovelView` コア 2 メソッド・ファセット責務境界 | ✅ 凍結 |
| game 供給サービスの一覧と責務 | ✅ 凍結 |
| ルーター所有権 | ✅ 凍結 |
| `se`/`bgm` 引数詳細・v1 インラインタグセット細部・各 View ファセットのメンバ | ⏳ 実装時に最終確定 |
