---
type: Design
title: 残論点（未決の設計判断）
description: ランナー API 凍結前に解消すべき未決事項と、後続で詰める設計トピック。
tags: [open-questions, todo, design]
timestamp: 2026-06-13T00:00:00Z
status: 保留
---

# 凍結前に決めるべき（高優先）

## コマンドスキーマのバージョニング/エイリアス戦略
[DSL 語彙: リッチ統一語彙を常設](/design/decisions/dsl-vocabulary.md) を選んだため、`[MRubyObject]
record struct` が `.mrb` バイトコードに焼かれる互換境界が広い。フィールド追加/リネーム/コマンド名変更で
既存 `.mrb` が静かに壊れる。バージョン番号付与・Ruby 名のエイリアス・移行期間の扱いを別ラウンドで設計する。

## コマンド名規約 ✅ 解決済み
→ [コマンド名規約と say スキーマ](/design/decisions/command-schema.md)（`say` 一本 + 糖衣、話者は id 基本のハイブリッド、`[Routes]` 規約）で確定。

## インラインタグ構文の正規定義
[行内インラインタグ](/design/decisions/inline-tags.md) のタグ記法（`{w=0.5}` 等）の正式仕様と、
パースを `Novel.Runtime` で行うか Ruby 文字列補間で表すか。タイプライタエンジンの契約に直結。

## ルーター所有権 ✅ 解決済み
→ [ルーター所有権](/design/decisions/router-ownership.md)（ノベル専用 Router を container 登録・ハンドラ DI 市民・
世界エフェクトは game 供給先への明示ブリッジ既定なし・provider 抽象は入れない）で確定。

# 中優先（コア周辺）

## エフェクトブリッジの await 意味論
脱出経路は [ルーター所有権](/design/decisions/router-ownership.md) で「game 供給先（`IWorldEffectSink` 等）への
明示ブリッジ・既定なし」と決定済み。**残るのは await 意味論**: apocalyptic 方式はハンドラが同期的に返る前提
（実装は shake 1 個のみ）。「次行前に終わる必要のある 2 秒ブラックアウト」のように完了待ちが要るエフェクトの
ordering/await 契約（ブロッキング/非ブロッキングの明示分類）が未設計。

## フロー/シーケンサの境界
ライブラリは `PlayAsync(key,ct)` + 完了結果のみ公開し、chapter/phase/auto-advance/retry は game 側、を
基本とする。ただし全 game が同種オーケストレーションを再実装するなら、汎用線形シーケンサを任意モジュールで
提供する案もある。「ライブラリが汎用シーケンサを持つ」か「完全 game 所有」かの線引きが未確定。

## セーブのスナップショット粒度（リプレイとの整合）
[実行モデル](/design/decisions/execution-model.md) のリプレイ前提で、何を履歴に記録し何を
`IStateStore` スナップショットに含めるか。チェックポイント境界の定義。

## 音声のスコープ
`say` に `voice_id` を常設（未配線時 no-op）か、完全別モジュールか。U1W の堅牢な VoiceService を
どこまで取り込むか。lipsync は対象外だがフックを残すか。

## MRuby ランタイムエラー処理/サンドボックス
全プロジェクトで `ExecuteAsync` 周りに try/catch ゼロ。シナリオ名 + Ruby バックトレースの surface、
著者向けエラーオーバーレイ、フェイルセーフ、シナリオが信頼境界内か（full `MRubyState` アクセスの是非）。

# 機能バックログ（v1 スコープ外だが将来検討）

- ロールバック（Ren'Py 式巻き戻し）。[実行モデル](/design/decisions/execution-model.md) のリプレイ基盤の上で将来。
- メッセージ窓の hide（CG 鑑賞）トグル。
- 選択肢のアフォーダンス: 無効/条件付き（grey out）・タイマー付き・一度のみ。
- アクセシビリティ: 文字サイズ・可読フォント・reduce-motion・hold-to-skip。
- 著者向けプレビュー/シーンジャンプ/分岐バリデータ（`Novel.Editor`）。
- `.rb` のホットリロード（編集 → 再 import なしの反復ループ）。
- `Novel.View` の外部依存スタンス（`Void2610.UnityTemplate`/LitMotion を依存とするか vendoring するか）。
- コンテキスト駆動セリフ選択（systemic barks、U1W 由来）を任意モジュール化。
