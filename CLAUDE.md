# novel-kit

VContainer + VitalRouter.MRuby 前提の、Unity 向け汎用ノベルゲームライブラリ。
既存 Unity プロジェクト群のノベル実装を 1 つの再利用可能ライブラリへ統合する。

現在は **設計フェーズ**。実装コードはまだ無く、設計判断・機能棚卸し・残論点を
`Knowledge~/design/` に OKF 形式で蓄積している段階。

## 前提

- スタック: VContainer / VitalRouter / VitalRouter.MRuby (MRubyCS) / UniTask / R3。
- シナリオは MRuby (`.rb` → `.mrb`) で記述。スプレッドシート読み込みは対象外。
- 設計の最新状態は必ず `Knowledge~/design/index.md` を起点に確認すること。

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
