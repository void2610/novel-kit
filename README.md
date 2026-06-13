# novel-kit

VContainer + VitalRouter.MRuby を前提とした、Unity 向けの汎用ノベルゲームライブラリ。

void2610 の複数 Unity プロジェクト（color-recollection / unity1week-2026-03 /
apocalyptic-apartment-hunting ほか）で個別実装されていたノベルゲーム機能を、
1 つの再利用可能なライブラリへ統合することを目的とする。

> **ステータス: 設計フェーズ**
> 既存実装の調査と設計判断の合意を進めている段階。実装コードはまだ無い。
> 設計判断・機能棚卸し・残論点は [`Knowledge~/design/`](./Knowledge~/design/index.md) に
> OKF (Open Knowledge Format) 形式で記録している。

## 前提スタック

| 役割 | パッケージ |
|---|---|
| DI | VContainer |
| コマンドバス | VitalRouter |
| シナリオスクリプト | VitalRouter.MRuby (MRubyCS) |
| 非同期 | UniTask |
| リアクティブ | R3 |

スプレッドシート等によるシナリオ読み込みは対象外。シナリオは MRuby (`.rb` → `.mrb`) で記述する。

## アーキテクチャ概要（提案）

4 アセンブリ + game 側配線層に分割する。

| asmdef | 内容 |
|---|---|
| `Novel.Commands` | `[MRubyObject] record struct : ICommand` 群。VitalRouter + MRubyCS.Serializer のみ依存 |
| `Novel.Runtime` | 純 C# コア。ランナー / `IScenarioSource` / `INovelView` / `[Routes]` ハンドラ / プリアンブル / タイプライタ / 状態ストア / バックログ |
| `Novel.View` | 任意の MonoBehaviour 参考 View（メッセージ窓・立ち絵・選択 UI 等）。game は無視して自前供給も可 |
| `Novel.Editor` | `.rb` ScriptedImporter + カタログ/検証 |

詳細は [`Knowledge~/design/architecture.md`](./Knowledge~/design/architecture.md) を参照。

## 知識ベース

設計・仕様・意思決定は [`Knowledge~/`](./Knowledge~/index.md)（OKF v0.1）で管理する。
運用規約は submodule [`Knowledge~/conventions/`](https://github.com/void2610/okf-conventions) を参照（読み取り専用）。

clone 時は submodule を含めること:

```bash
git clone --recurse-submodules https://github.com/void2610/novel-kit.git
# 付け忘れた場合:
git submodule update --init --recursive
```
