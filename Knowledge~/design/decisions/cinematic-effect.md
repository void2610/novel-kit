---
type: Decision
title: CinematicEffect 連携 — アセットを置くことが登録・対応表は持たない
description: cinematic-effect パッケージの CinematicSequenceAsset を、Resources/Novel/Effects/<key>.asset の配置規約だけでシナリオから呼べるようにする (opt-in アセンブリ)。DSL は world_effect と別の cinematic / cinematic_stop。停止は <key>_exit か Enter からの自動導出。標準 5 種は内蔵 sink。エディタの一覧・検証は規約フォルダを直接走査する。
tags: [decision, cinematic, effect, editor, opt-in, writer]
timestamp: 2026-08-29T00:00:00Z
status: 確定
---

# 状況

color-recollection は cinematic-effect の演出をシナリオから使うために C# を 3 つ書いていた:
key → `CinematicSequenceAsset` の対応表 SO (`CrWorldEffectLibrary`)、それを引く `IWorldEffectSink`
実装 (`CrWorldEffectSink`・標準 5 種はコードで組む)、DI 配線。しかしアセットは既に
`Vignette_Enter` / `Vignette_Exit` という名前を持っており、対応表はそれを `vignette` へ写しているだけで、
登録の二重化とズレの余地を作るだけだった。`CinematicSequenceAsset` 自体が Inspector で組める
「ノーコードの単位」なので、novel-kit に足りないのは配管だけ。

# 決定

- **アセットを置く = 登録。キー = アセット名。中間の表を一切持たない。**
  配置規約は `Resources/Novel/Effects/<key>.asset`。root は規約として固定する (可変にすると
  エディタの一覧・検証と実行時がズレる)。Addressables 派は `ICinematicSequenceLoader` を差し替える
  (その場合エディタの一覧には出ない — 明示した制約)。
- **DSL は `world_effect` とは別のメソッド** `cinematic :key` / `cinematic_stop :key` (ユーザー指示)。
  `world_effect` はゲーム側 sink の解釈、`cinematic` はアセット駆動で、意味が違うものを同じ語彙に
  乗せない。実体は `CinematicCommand{Key, Stop}` を `INovelCommandModule` で差し込む (コア無改変)。
- **停止は `<key>_exit.asset` → 無ければ Enter から導出**。Enter が `Play` (撃ちっぱなし = サスティン) した
  まま `Stop` していないエフェクトを、**その Play と同じ config で** `Stop` する。Director は Stop の config が
  null だと既定にリセットしてから止めるため、設計者が Play 側に詰めた exit 尺を引き継ぐには同じ config が
  要る。`PlayAndAwait` は一回完結で対象外。color-recollection の `_Exit` 10 個は全てこの形だった
  (実データで確認)。既に止まっているエフェクトには撃たない (`IsPlaying` で除外)。
- **標準 5 種 (shake / flash / fade_out / fade_in / blackout) はライブラリ内蔵のコード** (`IWorldEffectSink`
  実装として既定登録・後勝ちで差し替え可)。尺・強度を引数で受けるためアセットでは表現できない。
  ライブラリ内部の話なので「ゲーム側がコードを書かない」は守られる。
- **エディタの一覧・検証は規約フォルダを直接走査**する (画像タブと同じ方式)。`IWorldEffectSink` 等の
  interface に列挙を足さない (破壊的変更なし)。opt-in アセンブリがコアの editor に差し込めるよう
  `IProjectReferenceSection` / `IScenarioKeyExtension` の拡張点を切った。後者は語彙・記録モジュール・
  **preamble**・正解集合を提供する (preamble を渡さないとスタブ実行で糖衣が未定義 → no-op stub 化されて
  キーが記録されない。テストで発覚)。
- Director は `ICinematicRunner` で抽象化 (EditMode テストで MonoBehaviour を立てない)。シーンにあれば
  それを使い、無ければ生成する (各エフェクトは自己生成シングルトンで事前配置不要)。
- `NovelPlaybackProgress` を DI 登録し、モジュールが早送り状態と再生中キーを読めるようにした
  (`cinematic` は world_effect と同じく早送りでは再現しない)。

# 検討した代替案

- **対応表 SO + 汎用 sink (CR パターンのライブラリ化)**: ユーザーが「対応表を SO にするのはナンセンス」と
  棄却。アセット名が既にキーで、表は二重登録。
- **対応表に引数バインド (arg0 = Duration / Intensity)**: 上と同じ理由で棄却。引数が要る標準 5 種は
  コード内蔵で足りる。
- **シナリオ側でシーケンスを組む DSL** (`effect :vignette, intensity: 0.7`): 数値調整が作家側に移り、
  CR の「見た目は Inspector で詰める」方針に逆行。22 種のエフェクト × パラメータ表のバージョン結合も重い。不採用。
- **AssetPostprocessor で目録を自動生成** (置き場所を縛らない): 生成物の管理機構が増え「置いたら動く」の
  直接性が落ちる。Resources 規約で十分。不採用。
- **`world_effect` の未知キーをアセットへフォールバック**: 語彙の意味が混ざる。ユーザー指示で分離。
- **`RegisterNovelKit()` が package 存在時に自動配線**: asmdef の任意参照が Unity で扱いづらく、
  明示 1 行 (`RegisterNovelCinematicEffects()`) に留めた。

# 帰結

- color-recollection は `Settings/WorldEffects/` を `Resources/Novel/Effects/` へ移して `vignette` /
  `vignette_exit` に改名すれば、`CrWorldEffectLibrary` を削除でき、`CrWorldEffectSink` は
  `time_lapse` / `awake_cutin` / `saturation_pulse` の BGM 連動だけになる (標準 5 種は
  `BuiltinTransitionWorldEffectSink.TryBuild()` に委譲)。
- `.rb` 側は `world_effect :vignette, 0.7` → `cinematic :vignette`、`world_effect :vignette, 0` →
  `cinematic_stop :vignette` に書き換える (糖衣を preamble に置けば互換も取れる)。
