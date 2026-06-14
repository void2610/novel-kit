# novel-kit (Assets/Novel)

ゲーム内ノベルパート向けの再利用可能ライブラリ（UPM パッケージ `com.void2610.novel-kit`）。設計の全体像は `Knowledge~/design/` を参照。

## インストール（UPM）

> ⚠️ 依存は **Git UPM 6 本 + NuGet 4 本（NuGetForUnity 経由）** の混成です。UPM の `dependencies` では Git 依存を宣言できないため、
> **下記の前提パッケージを先に導入**してから novel-kit を追加してください。Unity **6000.3** 以上。

### 1. 前提 UPM パッケージ（`Packages/manifest.json` の `dependencies` に追記）

```json
"com.cysharp.r3": "https://github.com/Cysharp/R3.git?path=src/R3.Unity/Assets/R3.Unity",
"com.cysharp.unitask": "https://github.com/Cysharp/UniTask.git?path=src/UniTask/Assets/Plugins/UniTask",
"com.github-glitchenzo.nugetforunity": "https://github.com/GlitchEnzo/NuGetForUnity.git?path=/src/NuGetForUnity",
"jp.hadashikick.vcontainer": "https://github.com/hadashiA/VContainer.git?path=VContainer/Assets/VContainer",
"jp.hadashikick.vitalrouter.unity": "https://github.com/hadashiA/VitalRouter.git?path=src/VitalRouter.Unity/Assets/VitalRouter",
"jp.hadashikick.mrubycs-compiler": "https://github.com/hadashiA/ChibiRuby.git?path=src/MRubyCS.Unity/Assets/MRubyCS.Compiler.Unity#0.19.2"
```

### 2. NuGet パッケージ（NuGetForUnity の `Manage NuGet Packages` で導入）

`MRubyCS` 0.19.2 / `MRubyCS.Serializer` 0.16.0 / `VitalRouter` 2.2.0 / `VitalRouter.MRuby` 2.2.0
（依存の Microsoft.Extensions.* / System.* 等は自動で入る）。

### 3. novel-kit 本体（Git URL・サブパス指定）

Package Manager → *Add package from git URL...*：

```
https://github.com/void2610/novel-kit.git?path=Assets/Novel
```

### 4. TMP

参考 View（`NovelMessageView`）を使う場合は `Window > TextMeshPro > Import TMP Essential Resources` を実行。

利用手順は [`Docs/getting-started.md`](https://github.com/void2610/novel-kit/blob/main/Docs/getting-started.md)。

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
