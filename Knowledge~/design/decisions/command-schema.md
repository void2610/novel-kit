---
type: Decision
title: コマンド名規約と say スキーマ（最小 C# 層 + 糖衣）
description: 行コマンドは say プリミティブ1つ。キャラ名/narration は preamble 糖衣。話者は id 基本のハイブリッド。
tags: [decision, command, dsl, say, character, schema]
timestamp: 2026-06-13T00:00:00Z
status: 確定（VoiceId は音声トピックで確定予定）
---

# 状況

既存は行コマンドが `say`（color-recollection）と `speak`（unity1week/apocalyptic）に分かれ、話者の扱いも
表示名直書き（CR、空=ナレ）と id→`CharacterCatalog` 解決（apoc）が混在していた。これは著者 DSL の書き味・
`.mrb` 互換境界・語彙の一貫性を同時に規定するコア判断。[DSL 語彙](/design/decisions/dsl-vocabulary.md) を踏まえる。

# 決定

## C# コマンド層は最小、ergonomics は Ruby 糖衣へ

- 行コマンドは **`say` プリミティブ 1 つ**。`alice "..."`（キャラ名コマンド）と `narration "..."` は
  `preamble.rb` の糖衣が `say` に落とすだけで、**C# コマンドを増やさない**。
- 糖衣の実装変更は `.mrb` を壊さない（preamble は実行時に解決される間接層であり、scenario `.mrb` には
  糖衣メソッドの呼び出し位置しか焼かれない）。

```ruby
alice "やあ"                    # → say speaker: :alice              （カタログ解決）
alice "……", as: "？？？"        # → say speaker: :alice, display_as: "？？？"  （名前リビール）
say "謎の声", "そこにいるのは誰だ"  # → カタログ未登録 → "謎の声" を表示          （その場話者）
narration "——沈黙が流れた"        # → say speaker: nil                 （ナレーション）
```

## 話者はハイブリッド（id 基本 + 逃げ道）

```csharp
[MRubyObject]
readonly partial record struct SayCommand : ICommand {
    public string  SpeakerId { get; init; }   // "" / null = ナレーション
    public string? DisplayAs { get; init; }    // 任意: 表示名の上書き（「？？？」等）
    public string  Text      { get; init; }   // インラインタグ生テキスト（Runtime で字句解析）
    // VoiceId は音声トピックで確定（slot 予約）
}
```

解決規則（1 本に統一）:
1. `SpeakerId` が空 → **ナレーション**。
2. `SpeakerId` がカタログにある → カタログの表示名/立ち絵/既定ボイスを使う。`DisplayAs` があれば
   表示名だけ上書き（**名前リビール**対応）。
3. `SpeakerId` がカタログに無い → **id 文字列をそのまま表示名として出す**（**その場話者**対応）。

## その他

- ハンドラは **`[Routes]` 規約**（per-method `[Route]` 属性なしでボイラープレート削減）。
- `speak` エイリアスは当面なし（新規ライブラリなので `say` 一本）。

# 互換境界の所在（重要）

`.mrb` に実際に焼かれるのは **Ruby 側のコマンド名 / preamble メソッド名 / kwarg 名**であり、
C# プロパティ名は MRubyCS.Serializer の名前マッピングで吸収できる。したがって versioning が効くのは
これら Ruby 表層の名前であって C# の内部名ではない → [残論点](/design/open-questions.md) のスキーマ versioning へ。

# 帰結

- `ICharacterCatalog`（id→表示名/立ち絵/side/既定ボイス）が前提依存に加わる。未登録 id は id 文字列を
  表示名にフォールバックするため、カタログ未整備でも動く。[キャラクターモデル](/design/decisions/character-model.md)（単一スプライト）と整合。
- 表示名はカタログ/`ITextResolver` 経由で解決できるため、多言語化（[ローカライズ](/design/decisions/localization.md)）と
  改名が一点に集約される。
- `narration` は別コマンドにせず、空 `SpeakerId` として表現する。
- `VoiceId` フィールドの要否・形は音声トピックで確定（現状は slot 予約）。

# 検討した代替案

- **表示名一本**（`Speaker="アリス"` 直書き）: 無設定で書けるが、改名が全 .rb 置換・多言語破綻・立ち絵/音声連動不可。
- **id 一本（フォールバック無し）**: その場話者も必ずカタログ登録。規則は単純だが手間が増える。
- → ハイブリッドで両者の強み（一貫性/連動/改名/多言語 ＋ リビール/その場話者/無設定）を回収。
