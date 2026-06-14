# novel-kit

VContainer + VitalRouter.MRuby 前提の、Unity 向け汎用ノベルゲームライブラリ。
既存 Unity プロジェクト群のノベル実装を 1 つの再利用可能ライブラリへ統合する。

現在は **実装フェーズ**。確定済み 16 ADR は `Assets/Novel/` に主要部まで実装済みで、
EditMode テスト・サンプルシーンで全パイプラインの動作を確認済み。
設計との乖離は `Knowledge~/design/implementation-review.md` で監査・改善済み。

## 構成

- 本体は `Assets/Novel/` 配下。アセンブリ:
  - `Novel.Commands` — コマンド語彙（`SayCommand` ほか `NovelVocabulary`、`[MRubyObject] record struct : ICommand`）。
  - `Novel.Runtime` — 純 C# コア（`NovelScenarioRunner` / `NovelCommandHandler` / `NovelTagLexer` / `IBacklog`+`RingBufferBacklog` / 各 interface）。MonoBehaviour 非依存。
  - `Novel.View` — 参考 TMP View・`UnityFrameClock`・`RubyMarkup`（`<ruby>` 展開）・Resources ローダ・SO カタログ・dev 警告ファセット/`DebugNovelErrorHandler`（省略可）。
  - `Novel.VContainer` — `RegisterNovelKitCore()` 一括登録ヘルパ（純 `Novel.Runtime` 依存・View/Resources 非依存）。独自コマンドは `RegisterNovelCommand<TModule>()`（`INovelCommandModule`）で差し込む。
  - `Novel.View.VContainer` — `RegisterNovelKit()` 箱出し登録ヘルパ（Core + Resources ローダ + 警告ファセット + Debug エラーログ）。
  - `Novel.Editor` — シナリオ検証メニュー（`Novel/Validate Scenarios`）。
  - `Novel.Samples` / `Novel.Tests.EditMode` — 動作確認シーンと EditMode テスト。
- 利用手順は `Docs/getting-started.md`。

## 前提

- スタック: VContainer / VitalRouter / VitalRouter.MRuby (MRubyCS) / UniTask / R3。
- シナリオは MRuby (`.rb` → `.mrb`) で記述。スプレッドシート読み込みは対象外。
- 設計の最新状態は必ず `Knowledge~/design/index.md` を起点に確認すること。実装が
  ADR を上書き/精緻化した場合は該当 ADR の「実装で確定」節へ追認し、`log.md` に追記する。

<!-- OKF:START -->
## Knowledge~/ バンドル (OKF 知識ベース) の取り扱い

このプロジェクトは `Knowledge~/` に OKF (Open Knowledge Format) v0.1 形式の知識ベースを持つ。

- `Knowledge~/` 配下を読み書きする前に、必ず `Knowledge~/conventions/CONVENTIONS.md` (運用規約) を読み、それに従うこと。
- `Knowledge~/conventions/` は submodule (読み取り専用)。**ここを編集しないこと**。規約変更は中央リポジトリ okf-conventions で行う。
- 知識データ (`Knowledge~/lore/`, `design/`, `systems/` 等) はこのプロジェクトのリポジトリにコミットする。
- 概念ファイルを新規作成・更新したら、同階層 (無ければ `Knowledge~/log.md`) に ISO 8601 日付で 1 行追記すること。
- frontmatter の `type` は必須で空にしない。更新時は `timestamp` を現在時刻に更新する。
- 概念間リンクはバンドル基準の絶対パス (`/lore/...`) を優先する。
<!-- OKF:END -->
