# Changelog

本パッケージの変更履歴。形式は [Keep a Changelog](https://keepachangelog.com/ja/1.1.0/)、
バージョンは [Semantic Versioning](https://semver.org/lang/ja/) に従う（安定版 1.0.0 までは破壊的変更があり得る）。

## [Unreleased]

### Changed
- **破壊的**: 実表示中の背景キーを runtime が保持し、`NovelStateSnapshot` に含めるようにした。
  立ち絵やメッセージは途中復帰の早送りで再構築されるが、背景はシナリオ再生の外でロードすると
  bg が走らないため戻せない。キーを知っているのは runtime なのに追跡を game に強いており、
  game 側に「キーを保持して記録する」層 (Presenter) を書かせる原因になっていた。
  復元表示は `INovelScenarioRunner.RestoreBackgroundAsync`、bg コマンドを経ない差し替えは `ShowBackgroundAsync` で行う
  (どちらも保持を更新するため、この経路で変えた背景もセーブに乗る)。
  同梱の直列化 (`NovelSaveData` / `NovelSaveSerializer`) も背景キーを運ぶ。
  移行: game 側の背景キー追跡とセーブ項目を捨て、snapshot 経由にする。ロード直後に `RestoreBackgroundAsync` を呼ぶ。
- `ICharacterCatalog` の `DefaultPortraitKey` を runtime が使うようになった。say で `portrait` を明示しなければ、
  stage cast 在籍の話者の既定立ち絵が自動で出る (cast 外・カタログ未登録・ナレーションでは出ない)。
  これまで宣言できるだけで適用は game 任せだったため、各 game が `INovelView` 実装から `IPortraitDirector` を
  駆動する必要があり、スプライト解決も runtime を迂回していた。
  移行: game 側の自動表示ロジックを削除する (残すと二重に出る)。明示 `portrait` の優先順は変わらない。
- `IPortraitDirector` に `IsShowing` を追加し、既定実装が同一 slot への同一キー再表示を no-op にするようにした。
  say ごとに立ち絵が来るため、無条件に出し直すと表示時にフェードする View で演出が毎行再発火し、
  ロードも行数分走る (途中復帰の早送りでは表示を省いてもこの経路を通る)。stage 切替で slot が変わる再表示は
  抑止対象にしない。自前 Director 実装は `IsShowing` の追加が要る。
- **破壊的**: `NovelLine` に `PlainText` (タグと辞書ルビを除いた平文 = 既読 ID の算出基準) を追加。
  辞書ルビは lexer タグではなく TMP markup として重なるため、View が `Text` から平文を再計算するとよみが
  親文字と連なって残る。ルビ適用前に算出した平文を runtime が渡すことで、View 側の平文検査を成立させる。
  移行: View が `NovelTagLexer.ToPlainText(line.Text)` していた箇所を `line.PlainText` に置き換える。
- **破壊的**: スプライトを扱うファセットの引数を `Sprite?` から `ResolvedSprite` (論理キー + 解決済みスプライト) へ変更。
  `IPortraitView` / `IBackgroundView` / `ICenterImageView` / `IPortraitDirector` が対象。ロードは引き続き runtime が行い
  View に解決の裁量はないが、未解決と消去の区別・同一キー再表示の no-op 判定・game 側の状態記録 (セーブからの背景復元、
  イベント CG の解放) はキーを要するため、キーを併せて渡す。
  移行: `ShowAsync(Sprite? sprite, ct)` を `ShowAsync(ResolvedSprite x, ct)` にして `x.Sprite` / `x.Key` を使う。
  消去 (空キー) とロード失敗はどちらも `IsLoaded == false` のため、分けたい View は `IsCleared` を見る。
  空キーはロード対象でないため runtime が `ISpriteLoader` を呼ばない (実装側に空キーガードを強いない)。
- **破壊的**: アセットのロード手段を抽象化。`IScenarioSource` / `IPreambleSource` / ルビ辞書の実装を
  `ScenarioSource` / `PreambleSource` / `RubyDictionary` に一本化し、ロード戦略は `ITextAssetLoader` を
  コンストラクタで明示指定する。`ResourcesScenarioSource` / `ResourcesPreambleSource` / `ResourcesRubyDictionary` は削除。
  移行: `new ScenarioSource(new ResourcesTextAssetLoader(), "Scenarios/")`。
- **破壊的**: スプライトのロードを追加 (`ISpriteLoader`) したうえで、解決を runtime 側へ集約。
  `IPortraitView` / `IBackgroundView` / `ICenterImageView` / `IPortraitDirector` の引数を論理キー (`string`) から
  解決済み `Sprite?` へ変更した。View は「渡された絵を表示する」だけになり、キー解決は `NovelCommandHandler` が行う。
  これらのファセットと `PortraitLayout` は名前空間が `Novel.Runtime` → `Novel.Assets` に移動。
  移行: View の `ShowAsync(string key, ct)` を `ShowAsync(Sprite? sprite, ct)` に変え、内部のロード処理を削除する。
- `ScenarioSource` / `PreambleSource` / `RubyDictionary` / `RubyMarkup` / `NovelDisplayText` を `Novel.View` から
  `Novel.Runtime` へ移設 (純 C# のため。自前 View 実装者が参考 View アセンブリに依存せずに使える)。
- **破壊的**: 辞書ルビの適用位置が runtime (`NovelCommandHandler`) に移動。`IRubyDictionary` を DI 登録するだけで
  say / choose の表示テキストに自動適用される (既読 ID とバックログには混入しない)。
  移行: View 側の `ApplyTo` 呼び出しを**削除すること**。残すと二重適用になり、1 回目の出力に含まれる
  `<noparse>` 内の親文字が再マッチしてオーバーレイ markup が noparse 区間へ注入され、TMP タグがリテラル表示される。
  なお「初出のみ」の周回リセット (`ResetShown()`) は引き続き game 所有 (runner が勝手に呼ぶとシナリオ単位で
  リセットされセマンティクスが壊れるため)。

### Added
- `ITextAssetLoader` (テキスト) と `ISpriteLoader` (スプライト) のロード抽象、および Resources / Addressables 実装。
  Addressables 実装は `Novel.Addressables` asmdef に隔離し、`com.unity.addressables` 導入時のみコンパイルされる。
- `NovelDisplayText.Build` を公開。自前 View が `TextRevealEngine` の可視文字数と整合する TMP 文字列構築を再実装せずに済む。

### Fixed
- `RegisterNovelKit` が PlayerLoop 停止中に `async UniTask` を同期待ちしてデッドロックしていた問題。
- 親文字なし / 未クローズの `<ruby=よみ>` でよみが表示から脱落していた問題。

## [0.1.0] - 2026-06-14

### Added
- 初期リリース（実装フェーズ）。ゲーム内ノベルパート向けの再利用可能ライブラリ。
- コア（`Novel.Runtime`・純 C#）: `NovelScenarioRunner`（`PlayAsync` で 1 シナリオ再生）、`NovelCommandHandler`、
  `INovelView` 抽象、行内タグ lexer `NovelTagLexer`、UI 非依存のタイプライタ進行エンジン `TextRevealEngine`（`IFrameClock` 駆動）、
  単一 `IStateStore`（フラグ/変数/既読）、`IScenarioSource`/`IPreambleSource`/`ISaveStore`/`ICharacterCatalog`/`ITextResolver`/
  `IWorldEffectSink`/`INovelErrorHandler` 抽象。
- コマンド語彙（`Novel.Commands`）: `say/choose/flag/portrait/bg/still/se/bgm/wait/world_effect`。
- preamble 糖衣（Ruby）: `say/narration/chara/flag/val/flag?/portrait/bg/still/se/bgm/wait/choose` +
  世界エフェクト（`world_effect/shake/flash/fade_out/fade_in/blackout`）。
- 参考 View（`Novel.View`）: TMP メッセージ窓 + 選択肢 + shake/wave 頂点アニメ + auto/skip の `NovelMessageView`、
  Resources ローダ、`ScriptableCharacterCatalog`、dev 警告ファセット、`DebugNovelErrorHandler`。
- DI 統合: `RegisterNovelKitCore()`（`Novel.VContainer`・コアのみ）/ `RegisterNovelKit()`（`Novel.View.VContainer`・箱出し）。
- Editor: シナリオ検証メニュー `Novel/Validate Scenarios`。
- 実行モデルは前進専用 + チェックポイント割り切り（セーブは `PlayAsync` 境界）。MRuby 実行エラーは backtrace つきで surface。

### Notes
- 設計判断は `Knowledge~/design/`（OKF 知識ベース）、設計との乖離監査は `implementation-review.md` を参照。
- 安定版前のため、既読ハッシュ等の内部形式は予告なく変わりうる（移行機構なし）。
