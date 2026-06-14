# novel-kit (Assets/Novel)

ゲーム内ノベルパート向けの再利用可能ライブラリ。設計の全体像は `Knowledge~/design/` を参照。

## アセンブリ構成

| asmdef | 役割 |
| --- | --- |
| `Novel.Commands` | `[MRubyObject]` コマンド record struct（say/choose/flag/portrait/bg/still/se/bgm/wait/world_effect） |
| `Novel.Runtime` | 純 C# コア。`NovelScenarioRunner` / `NovelCommandHandler` / 抽象群 / インラインタグ lexer / 状態ブリッジ |
| `Novel.View` | TMP 参考 View・Resources ローダ・ScriptableObject カタログ（game は差し替え可） |
| `Novel.VContainer` | コア DI 統合（`RegisterNovelKitCore`）。純 `Novel.Runtime` のみ依存・View/Resources 非依存 |
| `Novel.View.VContainer` | 参考 View 込みの DI 統合（`RegisterNovelKit` = Core + Resources ローダ + 警告ファセット + ログ） |
| `Novel.Editor` | シナリオ検証メニュー `Novel/Validate Scenarios`（`ScenarioValidator`・全 `.rb` の `.mrb` 生成有無を検査）。`.rb`→`.mrb` のコンパイル自体は mrubycs-compiler パッケージが担当 |

## 使い方（VContainer）

```csharp
public sealed class NovelLifetimeScope : LifetimeScope
{
    protected override void Configure(IContainerBuilder builder)
    {
        builder.RegisterNovelKit();                                  // 既定実装を一括登録
        builder.RegisterComponentInHierarchy<NovelMessageView>().As<INovelView>();
        builder.RegisterInstance<ICharacterCatalog>(characterCatalog); // ScriptableCharacterCatalog 等
    }
}

// 任意の箇所から
var result = await runner.PlayAsync("intro", ct);   // NovelResult.Completed/Cancelled/Faulted
```

`INovelView` と `ICharacterCatalog` は game 固有のため別途登録する。省略可能サービス
（portrait/bg/audio/worldEffect/save/error）は no-op 既定で埋まる。

## シナリオ (.rb)

`Resources/Scenarios/` 配下に `.rb` を置くと mrubycs-compiler が `.mrb` を生成し、
`ResourcesScenarioSource` がロードする。糖衣は同梱 `Resources/Novel/Preamble.rb`。

```ruby
bg "room"
portrait :alice, "smile"
alice = "alice"
say alice, "やあ、<color=#f88>本気</color>なの？<w=0.5> 嘘でしょ"
narration "——沈黙が流れた"
n = choose(["はい", "いいえ"])
flag "answered", 1
say "", "（#{n == 0 ? '頷いた' : '首を振った'}）"
```

## 既知の fix-later

実装で生じた設計逸脱（ハンドラ/IStateStore の所有権、preamble 配布形態）は
`Knowledge~/design/open-questions.md` の「実装フェーズで生じた要再整理事項」を参照。
