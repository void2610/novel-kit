---
type: Decision
title: ルーター所有権はノベル専用 Router を container 登録
description: NovelLifetimeScope にノベル専用 VitalRouter を登録しハンドラは DI 市民。世界エフェクトは game 供給の送り先への明示ブリッジ（既定なし）。
tags: [decision, vitalrouter, vcontainer, router, di, bridge]
timestamp: 2026-06-13T00:00:00Z
status: 確定
---

# 状況

ルーター所有権は既存 3 プロジェクトで最大の不整合点。color-recollection は runner が `new Router()` を私有、
unity1week は container 所有（`RegisterVitalRouter`）、apocalyptic は private Router + 静的 `Router.Default` への
ブリッジ。ライブラリの DI エルゴノミクス・分離・世界エフェクト橋渡しを同時に規定する。

# 決定

**ノベル専用 Router を `NovelLifetimeScope` に container 登録し、`NovelCommandHandler` を DI 市民として
その Router にマップする**（unity1week 方式を踏襲しつつ「app グローバルではなくノベル専用スコープ」を明示）。

```
NovelLifetimeScope (game が追加する子スコープ)
├─ RegisterVitalRouter(routing => routing.Map<NovelCommandHandler>())  // ★ノベル専用 Router（Router.Default ではない）
├─ Register INovelView / IStateStore / ICharacterCatalog / IScenarioSource / ...
└─ Register NovelScenarioRunner (EntryPoint)
       └─ 注入された専用 Router に DefineVitalRouter(AddCommand<SayCommand>("say"), ...) して .rb を実行
```

- 専用 Router は静的 `Router.Default` でも game の gameplay Router でもない。
- 世界エフェクト（非ブロッキング。例: カメラ振動）は、静的 global ではなく **game が任意で供給する送り先
  （`IWorldEffectSink` 等）への明示ブリッジ**で脱出させる。既定はブリッジ無し。apocalyptic の `Router.Default`
  直結を一般化したもの。
- **`INovelRouterProvider` 抽象は当面入れない**（既定を 1 つに決め切る。必要が出たら後から足す）。

# 理由

- **分離**: consuming game が VitalRouter を gameplay に使うとは限らない（調査した非 MRuby 系の void-red /
  garden-gnome / otajam は VContainer + R3 で gameplay に VitalRouter 不使用）。専用 Router なら他用途・
  `Router.Default` と混線せず、VitalRouter を使わない game でもノベルが動く。
- **DI**: ハンドラと依存（[INovelView 等](/design/architecture.md)）を注入でき、テストは fake で差し替え。
- **ライフタイム**: スコープ束縛で生成/破棄が明確。
- 静的 `Router.Default` のハードコード回避で結合を下げる。

# 帰結

- game は `NovelLifetimeScope`（子スコープ）を追加し、専用 Router + 各サービス + runner を登録する
  （[アーキテクチャ](/design/architecture.md) の配線方針）。
- 世界エフェクトの **await 意味論**（ブロッキング/非ブロッキング・順序、完了待ちが要るエフェクトの扱い）は
  本決定の範囲外で別途 → [残論点](/design/open-questions.md)。本決定は「脱出経路は明示ブリッジ・既定なし」までを定める。
- 抽象（provider）を足すのは具体ニーズが出てから。

# 検討した代替案

- **runner-private `new Router()`**（color-recollection）: さらに分離が強くスコープ追加すら不要だが、ハンドラ依存を
  runner が手渡しになり DI 市民性を失う。不採用。
- **`Router.Default` を既定の世界エフェクト送り先にする**（apocalyptic そのまま）: 実装を活かせるが静的 global 結合。不採用。
- **`INovelRouterProvider` で両対応**: 未解決の対立を抽象で覆うと漏れる（disposal/橋渡し/サスペンド意味論が所有権で変わる）ため不採用。
