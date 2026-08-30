---
type: Decision
title: CinematicEffect 連携 — アセットを置くことが登録・対応表は持たない
description: cinematic-effect パッケージの CinematicSequenceAsset を、Resources/Novel/Effects/<key>.asset の配置規約だけでシナリオから呼べるようにする (opt-in アセンブリ)。DSL は world_effect と別の cinematic / cinematic_stop。停止は <key>_exit.asset (プロジェクトが置く。ライブラリは推測しない)。標準 5 種は内蔵 sink。エディタの一覧・検証は規約フォルダを直接走査する。
tags: [decision, cinematic, effect, editor, opt-in, writer]
timestamp: 2026-08-30T16:00:00Z
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
- **停止は `<key>_exit.asset`。ライブラリは止め方を推測しない**。当初「`_exit` が無ければ Enter が `Play` した
  ままのエフェクトを同じ config で `Stop` する導出」を実装した (CR の `_Exit` 10 個が全てその形で、Director が
  Stop の config null で既定リセットする都合も踏まえて config を引き継ぐ形にしていた) が、ユーザーが
  「止め方は演出の一部で、プロジェクトが自分で決めるもの。ライブラリが Enter の中身から代行する責務はない」と
  棄却。`_exit` が無ければ `EffectNotFound` を出すだけにした。一回で終わる演出に `_exit` は不要。
- **標準 5 種 (shake / flash / fade_out / fade_in / blackout) はライブラリ内蔵のコード** (`IWorldEffectSink`
  実装として既定登録・後勝ちで差し替え可)。尺・強度を引数で受けるためアセットでは表現できない。
  ライブラリ内部の話なので「ゲーム側がコードを書かない」は守られる。
- **エディタの一覧・検証は規約フォルダを直接走査**する (画像タブと同じ方式)。`IWorldEffectSink` 等の
  interface に列挙を足さない (破壊的変更なし)。opt-in アセンブリがコアの editor に差し込めるよう
  `IProjectReferenceSection` / `IScenarioKeyExtension` の拡張点を切った。後者は語彙・記録モジュール・
  **preamble**・正解集合を提供する (preamble を渡さないとスタブ実行で糖衣が未定義 → no-op stub 化されて
  キーが記録されない。テストで発覚)。
- **Director はそのまま使い、テスト都合の抽象・分離は置かない**。当初 `ICinematicRunner` で Director を偽装し、
  次にキー解決を `CinematicSequenceResolver` へ分離したが、いずれも「Director を立てずにテストしたい」だけが
  動機で本番の読み手には層が増えるだけ (ユーザー指摘で 2 度撤回)。モジュールが Director・ローダ・進行・
  ハンドラを直接持つ素直な形にし、テストは「テストが要るロジック」だけに絞る: 標準 5 種の組み立てと
  Validate 連携 (実際に欠陥を捕まえた)。「アセットを引いて RunAsync」は配管なので Director を立ててまで
  検証しない。Director (Awake で全エフェクトを構築する MonoBehaviour) はシーンにあればそれを使い、無ければ生成する。
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
- 2026-08-30 追記: CR の `whiteout_out` / `whiteout_in` (白版 fade) だけがゲーム側 sink のコード実装として残り、演出の置き場が
  3 系統に散って分かりにくいとの指摘で、`fade_out` / `fade_in` / `blackout` に色引数を足した (`fade_out 1.0, :white`)。
  `WorldEffectCommand` / `WorldEffect` に任意の `Color` (HTML 色名 / `#rrggbb`) を持たせ、内蔵 sink が `ColorUtility` で解釈する
  (未指定・不明は黒)。float 配列 `Args` に色を押し込む案は可読性で不採用。これで CR の sink は Director 外の自前演出 (time_lapse / awake_cutin) だけになる。
