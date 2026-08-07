---
type: Decision
title: 多言語化は原文キー + Unity Localization 統合（opt-in）
description: ITextResolver の seam に Unity Localization Package を差し込む。原文（日本語 raw テキスト）を String Table のキーにし、著者体験を変えずに多言語を非破壊後付けする。
tags: [decision, localization, i18n, unity-localization, text, asmdef]
timestamp: 2026-08-07T00:00:00Z
status: 暫定
---

# 状況

[ローカライズ v1 ADR](/design/decisions/localization.md) は「日本語のみ + 抽出フック」で確定し、
`ITextResolver`（既定は恒等変換）が say 本文・話者表示名・choose 選択肢に適用済み
（[API 表面](/design/api-surface.md)）。多言語は「このフックの差し替えで非破壊後付け」と約束されている。
本 ADR はその後付けの具体方策である。

前提と制約:

- 既存プロジェクト群は **Unity Localization Package (`com.unity.localization`) を積極採用**しており、
  Locale 管理・String Table・CSV/Google Sheets 連携・擬似ロケール等のツーリング資産がある。
  novel-kit の多言語化はこれを第一級の統合先とするのが自然。
- 著者体験（`.rb` にプロセ直書き）は変えない（v1 ADR の核心を維持）。
- `Novel.Runtime` は純 C# を維持し、Unity Localization へ直接依存しない。
  パッケージ未導入の game には一切の負担を掛けない（opt-in）。

# 決定

**原文キー方式 + opt-in アセンブリ `Novel.Localization`** を採る。

## 1. キー戦略: 原文（日本語 raw テキスト）をそのままキーにする

- `.rb` は日本語直書きのまま。String Table のキーに **resolve 前の原文**（インラインタグ込み、
  `SayCommand.Text` / `DisplayAs` / カタログ表示名 / choose 選択肢の生文字列）を使う。
- resolver は `Resolve(raw)` で raw をキーに現在ロケールのテーブルを引き、
  **ヒットしなければ raw をそのまま返す**（テーブル未整備・訳抜けでも従来挙動に落ちる健全なフォールバック）。
- 日本語ロケールはキー自体が表示文字列なのでテーブル値は不要（入れてもよい）。
- `ITextResolver` のシグネチャは不変。既存 seam をそのまま使い、コア API に変更は生じない。

```
say "本気なの？"                     # .rb は不変（著者体験そのまま）
   └ ITextResolver.Resolve("本気なの？")
        └ StringTable[ja→そのまま / en→"Are you serious?"]  ヒットしなければ原文
```

- タグ込み原文がキーになるため、**翻訳文も同じインラインタグを訳側で書く**
  （`<w>`/`<color>` 等の位置は言語ごとに移動してよい）。タグ整合の検証はバックログ（後述）。

## 2. opt-in アセンブリ `Novel.Localization`

`Novel.Addressables` と同型の versionDefines パターン
（`com.unity.localization` 導入時のみ `NOVEL_LOCALIZATION` でコンパイル）。

- `LocalizedTableTextResolver : ITextResolver` — 指定 String Table Collection を参照する同期 resolve。
- **初期化契約**: Unity Localization のテーブルロードは async（Addressables 経由）だが
  `Resolve` は同期のため、**`PlayAsync` 前にテーブルをロード済みにする**。実装側に
  `UniTask InitializeAsync(ct)`（`LocalizationSettings.InitializationOperation` + テーブル preload）を持ち、
  game が起動時ないしノベルパート入場前に await する。未初期化中の resolve は原文フォールバック。
- ロケール切替（`SelectedLocale` 変更）は**次の resolve から反映**。表示中の行・バックログの遡及再描画は
  しない（バックログは表示時ロケールで確定）。切替 UI・フォント切替は game/View 所有。
- DI 登録は game が `ITextResolver` を差し替える 1 行（`RegisterNovelKitCore` の既定 `IdentityTextResolver`
  を上書き）。登録ヘルパの糖衣は実装時に確定。

## 3. Editor 抽出ツール（`Novel/Localization/Extract Strings`）

- `.rb` を静的走査して say/narration/キャラ名糖衣/choose/`as:` の文字列リテラルを抽出し、
  String Table Collection へ **upsert**（既存訳は保持・新規キー追加・消滅キーはマークのみで削除しない）。
- 静的抽出の漏れ（糖衣の間接呼び・動的組み立て）は、dev 実行時の**未ヒット収集**で回収する:
  dev ビルドで resolver がテーブルミスした原文を記録し、レポート/テーブル追記できるようにする
  （警告ファセットと同じ dev 支援の位置付け）。

## 4. 既読 ID のロケール不変化（先行する唯一のコア変更）

現状 `NovelCommandHandler` は **resolve 後**テキストから既読 ID（`StableId`）を算出しており、
このままでは言語を切り替えると既読/スキップが言語ごとに分断する。

- **raw（原文）から算出する**よう変更する。既定の恒等 resolver では resolved == raw のため、
  既存プロジェクトの既読ハッシュは不変（セーブ互換を壊さない無害な移行）。
- choose の結果キーは明示 StateKey で既にロケール非依存（変更不要）。

## 5. 適用範囲と非対象

| 対象 | 扱い |
|---|---|
| say 本文・話者表示名・choose 選択肢 | 実装済み seam のまま原文キーで解決 |
| 既読/スキップ | 原文基準の ID でロケール不変（上記 4） |
| バックログ | 表示時ロケールで確定・遡及再翻訳しない（受容） |
| 辞書ルビ（`IRubyDictionary`） | JP 専用スタイリング。非 JP ロケールでは game が空辞書/差し替えで無効化（resolver と独立） |
| 画像内テキスト等のローカライズ済みアセット | AssetTable ベースの `ISpriteLoader` 実装は将来オプション（バックログ） |
| voice | 対象外のまま（[音声スコープ](/design/decisions/audio-scope.md)） |
| フォント/TMP フォールバック | View/game 所有（参考 View への支援はバックログ） |

## 6. 段階導入

1. **Phase 1（コア・挙動不変）**: 既読 ID を raw 基準へ移行。
2. **Phase 2**: `Novel.Localization` アセンブリ + `LocalizedTableTextResolver` + 初期化契約。
3. **Phase 3**: Editor 抽出ツール + dev 未ヒット収集レポート。
4. **Phase 4（バックログ）**: `#{}` 補間の救済（Smart String / 明示キー糖衣 `t()`）・
   訳し分けのための文脈付き resolve オーバーロード（speakerId 等を渡す default interface method）・
   ロケール横断のタグ整合検証（`Novel/Validate Scenarios` 拡張）・AssetTable スプライトローダ・
   locale-aware ルビ・参考 View のフォント切替支援。

# 既知の制約（v1 として受容）

- **`#{}` 補間入りテキスト**は実行時の最終文字列がキーになりテーブルに載らない。
  v1 ガイドラインは「ローカライズ対象のプロセに補間を使わない」（[Docs/scenario](/../Docs/scenario/index.md) へ
  実装時に明記）。救済は Phase 4。
- **同一原文の訳し分け不可**（同じ「はい」が文脈で yes/okay に分かれる等）。キーが原文のみのため
  同一訳になる。頻度は低いと見込み v1 は受容し、文脈付きオーバーロードを Phase 4 で検討。
- 語順の都合で分割行（複数 say に跨るセンテンス）の翻訳が不自然になり得るのは
  行単位提示モデルの本質的制約（全ノベルエンジン共通）で、方式によらない。

# 理由

- **原文キー**: 著者体験を一切変えず、抽出・フォールバック・翻訳者の可読性（キー列に原文が見える）が
  すべて自然に成立する。`ITextResolver` シグネチャ不変で「非破壊後付け」の約束をそのまま履行できる。
- **Unity Localization 統合**: 既存プロジェクトの採用実績とエディタツーリングをそのまま活用でき、
  novel-kit が足すのは resolver 1 実装と抽出ツールのみ。テーブル編集・翻訳ワークフロー・Locale 管理を
  ライブラリが再発明しない。
- **opt-in アセンブリ**: `Novel.Runtime` 純 C# と「未導入 game に無負担」を versionDefines の
  実績パターン（`Novel.Addressables`）で両立する。

# 検討した代替案

- **明示キー DSL（`say :line_001`）**: プロセ直書きの著者体験を破壊。[v1 ADR](/design/decisions/localization.md) で棄却済み。
- **ロケール別 `.rb` セット**（`IScenarioSource` がロケールで選択）: 翻訳同期が手動で、分岐/フラグ/既読の
  整合リスクを翻訳者が負う。主方式にしない（構造が言語で異なる特殊シナリオの逃げ道として将来併用は可能）。
- **ハッシュキー**（原文の StableId をキーに）: テーブルが翻訳者に不可読。原文キーで足りる。
- **カタログ SO に `LocalizedString` を直接埋める**: 表示名だけ別機構になり seam が割れる。
  `ITextResolver` 一点集約を維持する。
- **`ITextResolver` を async 化**: 行ごとの await はタイプライタ開始レイテンシとコア API 破壊の割に、
  テーブル preload で同期解決できるため不要。
