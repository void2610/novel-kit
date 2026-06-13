---
type: Decision
title: ライブラリ範囲はインターフェースコア + 参考 View 別パッケージ
description: Novel.Runtime は純 C# で INovelView 抽象のみ、Novel.View を任意依存の別パッケージとして提供する。
tags: [decision, scope, asmdef, view]
timestamp: 2026-06-13T00:00:00Z
status: 確定
---

# 状況

ライブラリが UI 提示層をどこまで含むかで、依存とオンボーディングコストが変わる。
color-recollection / apocalyptic-apartment-hunting は `INovelView` シームを実証済みで、
void-red には寄贈可能なほぼ完成形の参考 UI（タイプライタ・クロスフェード・選択 UI）が存在する。

# 決定

**インターフェースコア + 参考 View 別パッケージ** とする。

- `Novel.Runtime`: 純 C#。`INovelView` 抽象のみを公開し、UI 実装を持たない。
- `Novel.View`: 任意依存の別パッケージ。参考 MonoBehaviour View（窓・吹き出し・ナレ・立ち絵・
  背景・選択 UI）+ アニメツールキットを提供。game は使ってもよいし、自前 `INovelView` を供給してもよい。

```
Novel.Runtime  → 純 C#, INovelView だけ
Novel.View     → 参考 UI（任意, LitMotion 等に依存）
game: 参考 View を使う or 自前実装を供給
→ 最小プロジェクトは View 層の依存を引きずらない
```

# 理由

- 最小依存（コマンド + ランナーのみ）と batteries-included（即使える参考 UI）を両立できる。
- テストは fake `INovelView` で進行ロジックをヘッドレス検証できる。
- 参考 View の `Void2610.UnityTemplate` / LitMotion 依存をコアに波及させない。

# 帰結

- アセンブリ構成は [アーキテクチャ](/design/architecture.md) の 4 asmdef（Commands/Runtime/View/Editor）。
- `Novel.View` の外部依存スタンス（template/LitMotion を依存とするか vendoring するか）は要検討
  → [残論点](/design/open-questions.md)。

# 検討した代替案

- **batteries-included フルキット**: View 込みで template/LitMotion/R3 前提。即使えるが全 consumer を
  特定ユーティリティ群に結合。不採用。
- **コマンド + ランナーのみ**: UI 完全 game 側。最も薄く移植性高だが、毎回タイプライタ/選択 UI を
  再実装。参考 View を別パッケージで提供する本決定で代替。
