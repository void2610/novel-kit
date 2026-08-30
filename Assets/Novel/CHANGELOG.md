# Changelog

本パッケージの変更履歴。形式は [Keep a Changelog](https://keepachangelog.com/ja/1.1.0/)、
バージョンは [Semantic Versioning](https://semver.org/lang/ja/) に従う（安定版 1.0.0 までは破壊的変更があり得る）。

## [Unreleased]

### Added
- `Novel/Project Reference` に「コマンド」タブ。`RegisterNovelCommand<TModule>()` で登録したプロジェクト定義
  コマンドを、モジュールごとに名前と引数 (Ruby 側の名前と型) で一覧する。コピーは `screen_shake 0.0, false` のような
  呼び出し形 (引数は宣言順のプレースホルダー)。コマンド型・プロパティの `[NovelDescription("…")]` が説明として
  並ぶ。DI ビルド時に記録用 `INovelVocabulary` で語彙を読むため、一度再生すれば出る
  (糖衣 = preamble の関数名は `.mrb` から読めないため含まれない)

- `fade_out` / `fade_in` / `blackout` に色引数 (`fade_out 1.0, :white`)。`WorldEffectCommand` / `WorldEffect` に任意の `Color`
  (HTML 色名 / `#rrggbb`) を追加し、内蔵 `BuiltinTransitionWorldEffectSink` が解釈する (未指定・不明は黒)。
  白フェードのためにゲーム側が sink を書く必要が無くなった
- **CinematicEffect 連携 (opt-in・`com.void2610.cinematic-effect` 導入時のみコンパイル)**。
  `Resources/Novel/Effects/<key>.asset` に `CinematicSequenceAsset` を置くだけで、シナリオから
  `cinematic :key` / `cinematic_stop :key` で呼べる。対応表 (SO) は持たない — アセット名がキー
  - 停止 (`cinematic_stop :key`) は `<key>_exit.asset` を再生する。止め方も演出の一部としてプロジェクトが
    アセットで決め、ライブラリは Enter から推測しない
  - `RegisterNovelCinematicEffects()` (`Novel.CinematicEffect.VContainer`) で配線。Director はシーンに
    あれば使い、無ければ生成する。標準 5 種 (`shake` / `flash` / `fade_out` / `fade_in` / `blackout`) の
    `IWorldEffectSink` (`BuiltinTransitionWorldEffectSink`) も既定登録する (後勝ちで差し替え可)
  - `Novel/Project Reference` に「演出」タブ、`Novel/Validate Scenarios` に `cinematic` キーの検証を追加
- エディタの拡張点: `IProjectReferenceSection` (プロジェクトリファレンスのタブ追加) と
  `IScenarioKeyExtension` (Validate Scenarios の語彙・preamble・正解集合の追加)。opt-in アセンブリが
  `[InitializeOnLoad]` で登録する
- `NovelPlaybackProgress` を DI 登録し、独自コマンドモジュールが早送り状態と再生中シナリオキーを読めるようにした
  (`NovelScenarioRunner` のコンストラクタ引数 `progress` で共有)。`NovelDiagnostics` を public 化し
  `NovelIssueKind.EffectNotFound` を追加

- 例外にならない不具合の通知口 `INovelErrorHandler.OnRuntimeIssue(NovelIssueInfo)`（default 実装付きのため
  既存の実装は壊れない）。dev ビルドではライブラリが `Debug.LogWarning` も出すので、未実装でも無言にならない。
  現在の通知対象は `ScenarioNotFound` / `PreambleNotFound` / `SpriteNotFound`
  - `SpriteNotFound`: 画像キーを解決できないと従来は無言で空表示になり、「立ち絵が出ない」の原因が掴めなかった
- `NovelErrorInfo.SayNumber` / `LastSayText`: 落ちる直前に処理した say の通し番号と原文。`.mrb` に
  デバッグ情報が無く Ruby の行番号を得られないため、これがエラー位置の手掛かりになる
  （原文で `.rb` を検索すれば該当行に辿り着ける）。ログは
  `シナリオ 'chapter1' の 3 番目のセリフ「こんにちは」まで進んだ時点でエラー: ...` の形になる
- `Novel/Project Reference` の立ち絵・キーまわりを実用強化 (project-reference ADR)
  - `ISpriteKeyPrefix`: `ISpriteLoader` が「キーの前に付ける root」をエディタへ名乗る任意ファセット
    (`ResourcesSpriteLoader` は実装済み)。DI ビルド時にキャプチャし、ウィンドウが
    **シナリオにそのまま書けるキー** を表示するようになった。root 外のスプライトは
    「このシナリオからは読めない」と明示する。root を名乗らないローダでは従来どおり
    Resources 相対パスを表示し、その旨を断る
  - キャラタブが既定立ち絵 1 件ではなく **キャラごとの全立ち絵** を一覧するようになった。
    所在は「既定立ち絵と同じフォルダ」→「パスセグメントがキャラ id」→「ファイル名が `id_`」の順で推定する
  - 立ち絵にキャラを特定する部分を落とした短縮表記を併記 (表示専用。コピーされるのは常に実キー)
  - 全タブの全行にコピーボタンを追加 (画像キー・立ち絵キー・キャラ id・構図 `:single`・BGM / SE キー)
- 多言語対応 (opt-in・原文キー + 追従抽出。localization-unity-package ADR)。`com.unity.localization` 導入時のみ
  有効になる `Novel.Localization` / `Novel.Localization.Editor` を追加
  - `LocalizedTableTextResolver`: 原文 (タグ込み) をキーに String Table を引く `ITextResolver` 実装。
    未ヒット/未初期化/原文ロケールは原文フォールバック。`InitializeAsync` で preload・ロケール切替に自動追従・
    抽出漏れ収集用の `TextMissed` イベント
  - `Novel/Localization/Extract Strings...`（エディタツール本体は後続 PR）: `.rb` の差分抽出。原文変更を LCS + 類似度で検出し、KeyId を保った
    キーリネームで訳を追従 (タグのみ=訳保持 / 高類似=訳保持+fuzzy / リライト=旧訳退避・未訳化)。
    共有原文は分離し、消滅キーは削除せず deprecated マーク。適用前に移行レポートで人間が確認する
  - `MissingTextCollector` + `Novel/Localization/Report Missing Texts`（メニューは後続 PR。それまでは
    `Snapshot()` で取得）: dev プレイでのテーブルミス回収
  - `.rb` に出ないテキストも抽出対象: キャラカタログの表示名 (`ScriptableCharacterCatalog` アセット +
    DI ビルド時キャプチャの和集合) を疑似ファイルとして同じ追跡に載せる (キャラ改名で訳が追従する)

- テキスト変数 `%{key}` (遅延展開)。`narration "所持金は%{gold}Gだ"` のようにテンプレートのまま書き、
  表示時に `IStateStore` (`flag`/`val` 値) / game 供給 `ITextVariableProvider` (主人公名等・後勝ち登録) から
  差し込む。Ruby 補間 `#{}` と違い、多言語キー照合と既読 ID がテンプレート基準で成立する
  (`#{}` は値が変わるたび既読が割れる問題もあった)。未定義変数はプレースホルダ温存 + dev 警告。
  エスケープは `%%{`。ローカライズ非依存 (単言語でも使用可)
- `say` に `guest: true` を追加 (例: `say '？？？', 'ラー……', guest: true`)。カタログに載せない単発キャラを
  その行で明示でき、Validate Scenarios の「未定義のキャラ id」から外れる
  (未明示・未登録の id は従来どおり誤記として警告)。
  この形の話者 id は未登録のまま表示名として画面に出るため、ローカライズ抽出 (`Extract Strings`) の対象になる

- `AudioKeyInfo` に `Asset` (任意・エディタ試聴用) を追加。`IAudioChannel.EnumerateKeys()` が保持済みの
  AudioClip を渡すと、プロジェクトリファレンスの試聴がキー体系に依存せず効く (エディタ側で GUID 永続化)。
  Novel.Runtime にアセット型を持ち込まないため型は `object`
- `ICharacterCatalog.EnumerateEntries()` を追加。コード実装のカタログも
  プロジェクトリファレンス / シナリオ検証のキャラ情報源になる (DI ビルド時キャプチャに `Characters` を追加。
  `ScriptableCharacterCatalog` は実装済み)。アセットカタログが無い場合、ウィンドウはキャプチャを表示し、
  検証はアセットとキャプチャの和集合を正とする
- DI 登録ヘルパー (`RegisterNovelKitCore` / `RegisterNovelKit` / `RegisterNovelCommand`) に `Lifetime` 引数を追加。
  既定は従来どおり `Lifetime.Singleton`。親スコープで一度登録してシーンごとに独立したインスタンスを持たせたい
  game は `Lifetime.Scoped` を指定する。あわせて `Router` / `IBacklog` の登録をインスタンス登録から
  ファクトリ登録へ変更した (共有インスタンスのままでは lifetime 指定が効かないため)。
  `Lifetime.Transient` は Router と runner が注入点ごとに分裂して進行と `CaptureState` が無言で食い違うため受け付けない

- `ITextAssetLoader` (テキスト) と `ISpriteLoader` (スプライト) のロード抽象、および Resources / Addressables 実装。
  Addressables 実装は `Novel.Addressables` asmdef に隔離し、`com.unity.addressables` 導入時のみコンパイルされる。
- `NovelDisplayText.Build` を公開。自前 View が `TextRevealEngine` の可視文字数と整合する TMP 文字列構築を再実装せずに済む。

### Changed
- **破壊的**: `INovelCommandModule.RegisterVocabulary` の引数が `MRubyState` から `INovelVocabulary` になった。
  `state.AddCommand<T>("name")` → `vocabulary.Add<T>("name")` に書き換える。VitalRouter.MRuby には登録済み語彙を
  読み戻す API が無いため、束縛口を novel-kit 側で持ち、エディタが MRubyState を作らずに語彙を記録できるようにした。
  `RegisterVocabulary` は登録以外の副作用を持たない契約 (記録用実装でも呼ばれる)

- 既読 ID (スキップ判定) を resolve 後テキストから **resolve 前の原文**基準へ変更。ロケールを切り替えても
  既読が分断しない。既定の恒等 resolver では従来と同一ハッシュのためセーブ互換は不変
- Validate Scenarios: 未登録コマンドで停止する代わりに、NoMethodError のコマンド名を no-op stub として
  定義し直して流し直し、以降の行も検証を続けるようにした。読み飛ばした名前は
  「game 独自コマンドなら正常。誤記でないか確認」として警告に列挙する
- **破壊的**: `IPortraitDirector` に `HasPortrait` を追加し、話者の既定立ち絵を「そのキャラの立ち絵がまだ
  出ていないときだけ」適用するようにした。従来は `portrait` で指定した表情が直後の `say` で既定の顔に
  戻され、表情指定が毎行消えていた。既定実装は slot を別キャラに奪われた記録も落とすため、
  cast 外 `portrait` の slot 0 フォールバックで画面から消えたキャラは表示中とみなされない。
  移行: 自前の `IPortraitDirector` 実装に `HasPortrait` の追加が要る。

- **破壊的**: `IAudioChannel.EnumerateKeys()` / `ICharacterCatalog.EnumerateEntries()` の default 実装 (空) を削除。
  実装忘れが「再生しても一覧に出ない」という沈黙の空目録になるため、明示実装をコンパイルエラーで要求する。
  移行: 一覧を提供しない実装にも `=> System.Array.Empty<...>()` を明示的に書く。
  `IPortraitChannel.EnumerateLayouts()` の default (標準 5 構図) は従来どおり。
- `Novel/Project Reference` ウィンドウを見やすくした。折りたたみ縦積みから種別ごとのタブ (キャラ / 画像 / 構図 / BGM・SE) へ変更。
  画像キーと既定立ち絵はサムネイル付き (サイズはツールバーのスライダーで変更・行クリックでアセットを ping)、
  構図はスロット配置のミニ図付き、音キーは ▶ でその場で試聴できる (`AudioKeyInfo.Asset` のキャプチャ参照が
  最優先。無ければ Resources 相対パスとして完全一致 → 後方一致で照合し、曖昧・解決不能なら試聴ボタンは無効のまま)
- **破壊的**: スプライトを扱うファセットを整理した。
  - `IPortraitView` / `IBackgroundView` / `ICenterImageView` を `IPortraitChannel` / `IBackgroundChannel` /
    `ICenterImageChannel` へ改名 (`Null*` / `Warning*` 実装も同様)。実装は MonoBehaviour の View とは限らないため、
    既にある `IAudioChannel` / `IWorldEffectSink` と語彙を揃える。`INovelView` は同梱 View が実装するので据え置き
  - `IBackgroundChannel` からイベント CG を `IStillChannel` として切り出した。背景と CG はレイヤーも game 側の
    関心も別で、1 つの実装に抱えさせると肥大化する
  - `IPortraitChannel.ShowAsync` から未使用の `character` を削除
  - `ResolvedSprite` でキーを渡す点は維持。novel-kit 自身はシナリオ再生の外を関知しないが、CG 回収の記録や
    シナリオ外での背景維持といった拡張を game 側でできるようにしておくため
  移行: 型名とシグネチャの置換。`still` を出す game は `IStillChannel` を実装して登録する。
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
  `IPortraitChannel` / `IBackgroundChannel` / `ICenterImageChannel` / `IPortraitDirector` が対象。ロードは引き続き runtime が行い
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
  `IPortraitChannel` / `IBackgroundChannel` / `ICenterImageChannel` / `IPortraitDirector` の引数を論理キー (`string`) から
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

### Fixed
- **Ruby の backtrace が一度も surface されていなかったのを修正**。`NovelErrorReport` は
  `GetBacktraceString()` を例外型から探していたが、実際は `MRubyState` 側のメソッドで常に見つからず、
  作家には C# のスタックトレースだけが届いていた。`例外.ExceptionObject.Backtrace.ToString(state)` へ
  辿り直した（`MRubyState.GetBacktraceString()` は VM を抜けた後だと空を返すため使えない）
- **`RegisterNovelKitCore` の既定エラーハンドラが無音だった**のを修正。error-handling ADR は既定を
  `DebugNovelErrorHandler` に変えたと記録していたが、実際に変わっていたのは View ヘルパだけで、
  Core だけを使う構成では MRuby 例外が一切ログに出なかった。`DebugNovelErrorHandler` を
  `Novel.View` から `Novel.Runtime` へ移し、Core の既定に据えた（**破壊的**: 名前空間が変わる）
- **シナリオが見つからないときに `NovelResult.Completed` を返していた**のを `Faulted` に変更（**破壊的**）。
  キーの誤記やアセット未配置が「一瞬で正常終了」に見え、最も原因を掴みにくい失敗になっていた

- プロジェクトリファレンスのキャプチャが、novel 未配線スコープのビルド (タイトル画面のシーンや EditMode テスト等)
  で空に上書きされ、音キー・キャラ情報がすぐ消えていた問題。エディタ側ストアが種別ごとにマージするようにした
  (空の種別は「列挙未提供」として以前の実データを保持。Edit Mode のコンテナビルドは採用しない)。
  併せて試聴用の AudioClip は常に GUID からアセット実体を引くようにし、再生終了でランタイム参照が
  破棄されても試聴が生き続けるようにした。
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
