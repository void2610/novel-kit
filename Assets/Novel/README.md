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
| `Novel.Runtime` | 純 C# コア。`NovelScenarioRunner` / `NovelCommandHandler` / 抽象群 / インラインタグ lexer / 状態ブリッジ / 供給源 (`ScenarioSource` 等) とルビ展開 |
| `Novel.View` | TMP 参考 View・Resources ローダ・ScriptableObject カタログ（game は差し替え可） |
| `Novel.VContainer` | コア DI 統合（`RegisterNovelKitCore`）。純 `Novel.Runtime` のみ依存・View/Resources 非依存 |
| `Novel.View.VContainer` | 参考 View 込みの DI 統合（`RegisterNovelKit` = Core + Resources ローダ + 警告ファセット + ログ） |
| `Novel.Assets` | Unity アセットのロード抽象（`Assets/Novel/AssetLoaders/`）。`ISpriteLoader` と Resources 実装（立ち絵/背景/CG を表示する game 側 View 向け） |
| `Novel.Addressables` | `ITextAssetLoader` / `ISpriteLoader` の Addressables 実装。`com.unity.addressables` 導入時のみコンパイルされる（versionDefines ゲート） |
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
`ScenarioSource` がロードする。糖衣は同梱 `Resources/Novel/Preamble.rb`。

### デモシナリオ（現行コマンドひと通り）

```ruby
# ===== 登場人物と初期セットアップ =====
chara :alice                    # 以降 alice "…" が say "alice", "…" の糖衣になる
chara :bob

bgm "town"                      # BGM 再生（引数なしの bgm で停止）
bg "classroom"                  # 背景切替
stage :pair, [:alice, :bob]     # レイアウトと立ち位置。配列は先頭から slot 0,1,…
                                # 明示するなら stage :pair, alice: 1, bob: 0

# ===== セリフと行内タグ =====
alice "やあ、<color=#8cf>novel-kit</color> のデモへようこそ。"   # TMP タグはそのまま素通し
alice "文字は<w=0.4>少しずつ<w=0.4>出る。<p>ここでクリック待ち。"  # <w>=秒待ち / <p>=クリック待ち
bob "<speed=2x>早口で喋る。</speed><fast>ここからは一気に表示。"
bob "<shake>揺らす</shake>のも <wave>波打たせる</wave>のも行内タグ。"
narration "<ruby=ふりがな>振り仮名</ruby>も行内で振れる。"

say :alice, "…実はね。", "alice/serious", display_as: "？？？"   # 立ち絵と表示名を同時指定
bob "（声だけ聞こえる）", as: "少年の声"                          # 表示名だけ上書き

# ===== 演出コマンド =====
portrait :alice, "alice/smile"  # 立ち絵だけ差し替え
se "door"                       # 単発 SE
se_loop "step", 0.3, 3          # SE を 0.3 秒間隔で 3 回
wait 0.5                        # 明示ウェイト
shake 1.5                       # 世界エフェクト（カメラ等ゲーム本体への作用）
flash 0.2                       # world_effect :flash の糖衣。独自キーは world_effect :zoom, 2.0

# ===== イベント CG =====
hide_message_window             # 全画面で見せるためウィンドウを隠す
still "cg/first_meeting"        # 一枚絵
wait 1.0
show_message_window
image "memo"                    # 中央画像（回想メモ等）
hide_image

# ===== 選択肢とフラグ =====
alice "信じてくれる？"
n = choose(["信じる", "疑う"], key: :trust)   # key: を渡すと安定キー＝セーブ対象になる
                                              # 省略時は一時キー（__ 始まりでセーブ除外）
if n == 0                                     # 戻り値は直後に使う（後述）
  flag :trusted, 1                            # フラグ書き込み
  alice "ありがとう。", as: "アリス（笑顔）"
else
  alice "…そう。", "alice/sad"
end

# cmd を跨ぐとローカル変数は失われる（mruby Fiber の resume 挙動）。
# 後から読むときはローカルではなく state を読む
narration "選択=#{val(:trust)} / 信頼済み=#{flag?(:trusted)}"

# ===== 場面転換と終幕 =====
clear_message                   # 直前のセリフを残さずに場面を切る
exit_chara :bob                 # 一人だけ退場
fade_out 1.0
clear_stage                     # cast を全消し（layout は維持）
bg "night_street"
fade_in 1.0
alice "——また明日。"
bgm                             # 引数なしで BGM 停止
blackout
narration "（おわり）"
```

背景・立ち絵・CG のキー（`"classroom"` や `"alice/smile"`）が何を指すかは game 側の View 実装が決める。
ライブラリはキー文字列を渡すだけで、解決は `ISpriteLoader` 等に任せる（後述）。

## アセットのロード手段（Resources / Addressables）

シナリオ・preamble・ルビ辞書のロード手段は `ITextAssetLoader` で明示する。
Resources なら `ResourcesTextAssetLoader`、Addressables なら `com.unity.addressables` を導入すると
`Novel.Addressables` asmdef が有効になり `AddressablesTextAssetLoader` を使える:

```csharp
// Resources
var loader = new ResourcesTextAssetLoader();      // キーは Resources 相対パス
// Addressables
// var loader = new AddressablesTextAssetLoader();   // キーはアドレス

builder.RegisterInstance<IScenarioSource>(new ScenarioSource(loader, "Scenarios/"));
builder.RegisterInstance<IPreambleSource>(new PreambleSource(loader));

// ルビ辞書はロードが非同期になるため、起動時に読み込んでからインスタンス登録する
var ruby = new RubyDictionary();
await ruby.LoadFromAsync(loader, "Novel/ruby", ct);
builder.RegisterInstance<IRubyDictionary>(ruby);
```

シナリオの `.mrb` は `.rb` アセットのサブアセットなので、`.rb` 本体をアドレス登録すればよい
（ローダーがサブアセットから `.mrb` を取り出す）。

### スプライト（立ち絵 / 背景 / CG）

立ち絵・背景・CG の表示 View は game 所有で、novel-kit はキー文字列を渡すだけ。
そのキーからスプライトを引く部分は `ISpriteLoader` を使うと Resources / Addressables を差し替えられる:

```csharp
ISpriteLoader loader = new ResourcesSpriteLoader("Novel/");      // Resources/Novel/<key>
// ISpriteLoader loader = new AddressablesSpriteLoader("Novel/"); // アドレス "Novel/<key>"

var sprite = await loader.LoadAsync(portraitKey, ct);
```

テキストと違いスプライトは表示中ずっと参照が生きている必要があるため、ハンドルはローダーが保持する。
シナリオ終了などの区切りで `ReleaseAll()` を呼んで解放する（Resources 実装では no-op。ロード中のものは対象外で完了後に次回の解放対象になる）。

`spriteMode=Multiple` のアセットは扱いが実装で非対称（Resources 実装は `LoadAll` の先頭スライスを返すが順序は保証されず、
Addressables 実装はスライス単位のアドレス指定が要る場合がある）。単一スプライトでの利用を推奨する。

## 既知の fix-later

実装で生じた設計逸脱（ハンドラ/IStateStore の所有権、preamble 配布形態）は
`Knowledge~/design/open-questions.md` の「実装フェーズで生じた要再整理事項」を参照。
