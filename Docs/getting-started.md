# Getting Started — novel-kit

ゲーム内ノベルパートを動かすまでの最小チュートリアル。

> ⚠️ **API は流動的です。** 本ライブラリは実装初期段階で、型名・メソッド・DSL は予告なく変わります。
> 本書は「今動く最小手順」を示すもので、安定 API のリファレンスではありません。

---

## 1. 前提

Unity 6000.3 / 以下のパッケージが導入済みであること（リポジトリ同梱の `Packages/manifest.json` 参照）。

- VContainer / VitalRouter (+ VitalRouter.MRuby) / MRubyCS / UniTask / R3 / NuGetForUnity
- TMP を使う場合は **TMP Essential Resources** を import（`Window > TextMeshPro > Import TMP Essential Resources`）

`Assets/Novel/` 配下が novel-kit 本体です。

---

## 2. シナリオを書く

`Resources/Scenarios/` に `.rb` を置くと自動で `.mrb`（バイトコード）にコンパイルされます。
糖衣（`say` 等）は同梱の `Resources/Novel/Preamble.rb` が定義済みなので、そのまま関数として書けます。

> ✍️ シナリオの書き方の体系的な解説（全命令・全タグ・分岐・落とし穴）は
> **[シナリオライターズガイド](./scenario/index.md)** にあります。本節は最小限の紹介です。

```ruby
# Assets/.../Resources/Scenarios/intro.rb
chara :alice            # 登場キャラの分だけ糖衣を生やす（プロジェクト固有のキャラ差はここで吸収）
bg "room"
portrait :alice, "smile"
alice "やあ、ようこそ <color=#8cf>novel-kit</color> へ。"
alice "文字は<w=0.4>少しずつ<shake>出る</shake>よ。"
narration "——彼女はこちらを見た。"
n = choose(["はい", "いいえ"])
flag "answered", 1
if n == 0
  alice "うれしい！", as: "アリス（笑顔）"   # as: で表示名を上書き（名前リビール）
else
  alice "そっか……。"
end
```

### 使える命令（preamble の糖衣）

| 命令 | 意味 |
|---|---|
| `chara :alice` → `alice "…"` | **キャラ名糖衣**。登場キャラの分だけ `chara :id` を書くと、以降 `id "…"` で話せる。`as:` で表示名上書き |
| `say(speaker, text, display_as: nil)` | セリフ。`speaker` 省略相当（空文字）でナレーション。`display_as:` で表示名を上書き（名前リビール） |
| `narration(text)` | ナレーション（話者なし） |
| `choose(["A","B"], key: nil)` | 選択肢を出し、選んだ index（0 始まり）を返す。`key:` 省略時は一時キー（セーブに残さない）、`key: :name` で安定キー（跨シナリオで残す） |
| `flag(key, value=1)` | フラグ/変数を設定 |
| `val(key)` / `flag?(key)` | 変数 read 糖衣（未設定は 0 / 0 以外を真）。例: `if flag?(:answered)` |
| `portrait(char, key)` | 立ち絵（単一スロット差し替え） |
| `bg(key)` / `still(key)` | 背景 / 一枚絵 |
| `se(key)` / `bgm(key)` | 効果音 / BGM（`bgm("")` で停止） |
| `wait(seconds)` | 明示待機 |
| `shake` / `flash` / `fade_out(s)` / `fade_in(s)` / `blackout` | 世界エフェクト（カメラ/画面）。game が `IWorldEffectSink` を供給したときだけ作用（未供給は no-op） |

> キャラ名糖衣は `chara :alice` を書いて生やします（`method_missing` ではなく `define_method` 方式）。同じことは `say "alice", "…"` でも書けます。話者 id はカタログで表示名/立ち絵に解決されます。

### 本文のインラインタグ

TMP リッチテキストと同じ `<...>` 記法。

| タグ | 効果 |
|---|---|
| `<w=N>` | N 秒待つ |
| `<p>` | クリック待ち |
| `<speed=2x>…</speed>` | 区間の表示速度 |
| `<fast>` | 以降を即時表示 |
| `<shake>…</shake>` / `<wave>…</wave>` | 文字を揺らす |
| `<ruby=よみ>漢字</ruby>` | ふりがな（よみを親文字の上に重ねて表示。参考 View が TMP 座標で展開） |
| `<color>` `<size>` `<b>` `<link>` など | TMP スタイル（そのまま反映） |
| `<noparse>…</noparse>` | リテラル表示 |

> ふりがなの「初出のみ表示」や辞書一括付与といった作品固有の制御は game 側で `<ruby=…>` タグを挿入して実現します
> （ライブラリはタグの展開だけを担います）。表示済みセリフは `IBacklog`（既定 `RingBufferBacklog`・200 行・rich 保持）に
> 自動で積まれ、バックログ UI から `IBacklog.Entries` を読めます。消去契機（リトライ/ロード/章移動）は game が `Clear()` します。

話者 `:alice` 等の表示名は `ICharacterCatalog` で解決します（未登録なら id をそのまま表示）。

### プロジェクト独自コマンドを足す

組込語彙で足りないゲーム固有コマンド（gameplay への作用・独自演出など）は、`INovelCommandModule` を実装して
差し込みます。1 クラスに「語彙束縛（Ruby 名→C# 型）」と「ハンドラ（`On(...)`）」を同居させます。

```csharp
using Novel.Runtime;
using Novel.Commands;       // ICommand
using MRubyCS.Serializer;   // [MRubyObject]
using VitalRouter;          // [Routes] / ICommandSubscribable

[MRubyObject]
public readonly partial record struct ScreenShakeCommand : ICommand
{
    public float Power { get; init; }
}

[Routes]
public sealed partial class GameplayNovelCommands : INovelCommandModule
{
    readonly ICameraShaker _shaker;
    public GameplayNovelCommands(ICameraShaker shaker) => _shaker = shaker;

    public void RegisterVocabulary(INovelVocabulary vocabulary) => vocabulary.Add<ScreenShakeCommand>("screen_shake");
    public IDisposable MapHandlers(ICommandSubscribable router) => MapTo(router);   // VitalRouter 生成

    public async UniTask On(ScreenShakeCommand cmd, CancellationToken ct) => await _shaker.ShakeAsync(cmd.Power, ct);
}
```

配線は LifetimeScope で 1 行足すだけです（runner が `IEnumerable<INovelCommandModule>` として集約注入します）。

```csharp
builder.RegisterNovelKit();
builder.RegisterNovelCommand<GameplayNovelCommands>();   // 追加
```

シナリオからは `cmd :screen_shake, power: 2.0` で直接呼べます。糖衣 `def screen_shake(p); cmd :screen_shake, power: p.to_f; end`
を足したい場合は、その `.rb`（→`.mrb`）を **追加の `IPreambleSource`** として登録します（組込糖衣はそのまま残り、登録順で
後に評価されます）。MRubyCS は実行時に Ruby ソースを eval できないため、糖衣もインポート時コンパイル経由の追加プリアンブルで供給します。

---

## 3. シーンに配線する

最小構成は VContainer の `RegisterNovelKit()` 一発 + game 固有の 2 つだけ登録します。

```csharp
using Novel.Integration;   // RegisterNovelKit
using Novel.Runtime;
using Novel.View;
using VContainer;
using VContainer.Unity;

public sealed class NovelLifetimeScope : LifetimeScope
{
    [SerializeField] NovelMessageView view;       // 参考 View（自前実装でも可）
    [SerializeField] ScriptableCharacterCatalog catalog;

    protected override void Configure(IContainerBuilder builder)
    {
        builder.RegisterNovelKit();                          // 既定実装を一括登録
        builder.RegisterComponent(view).As<INovelView>();    // 表示
        builder.RegisterInstance<ICharacterCatalog>(catalog);// 話者カタログ
    }
}
```

`RegisterNovelKit()` が登録するもの: ランナー・Router・`Resources` ローダ・preamble ローダ・
テキスト解決（恒等）・各 View ファセットの no-op 既定・エラーハンドラ（dev では `Debug.LogError` で
シナリオ名 + Ruby backtrace を出力）。`INovelView` と `ICharacterCatalog` だけは game が登録します。
`RegisterNovelKit()` は `Novel.View.VContainer` アセンブリにあるので、game の LifetimeScope の asmdef は
`Novel.View.VContainer`（と `Novel.View`）を参照してください。

> **コアだけ欲しい場合**（自前 View / Addressables 等の独自ローダで、参考 View・Resources・TMP に依存したくない）は、
> `Novel.VContainer` の `builder.RegisterNovelKitCore()` を使います。これは純 `Novel.Runtime` だけに依存し、
> `IScenarioSource` / `IPreambleSource` は game が登録します（`RegisterNovelKit()` は内部でこの Core を呼んでいます）。

> **シーンごとに独立させたい場合**は `Lifetime.Scoped` を渡します。親（root）の LifetimeScope で一度
> `RegisterNovelKit(lifetime: Lifetime.Scoped)` を呼んでおくと、解決したシーンのスコープが自分用のインスタンスを
> 作り、そのスコープの破棄と一緒に片付きます。既定の `Lifetime.Singleton` はアプリ全体で 1 つになります。
> `Lifetime.Transient` は Router と runner が注入点ごとに分裂するため受け付けません。
> なお `RegisterNovelKit()` が登録する辞書ルビは Scoped だとシーンごとに作り直されるため、`:first`（初出のみ）も
> シーン単位になります。ラン全体で一度だけにしたい場合は `IRubyDictionary` を game 側で後勝ち登録してください。

> **ファセット/サービスの差し替えは `RegisterNovelKit()` の後に登録**してください（後勝ち）。立ち絵・背景・音声・
> セーブ・世界エフェクトを供給するときは `builder.Register<IPortraitView, MyPortraitView>(...)` 等を後に書きます。
> 未供給のまま `portrait`/`bg`/`se` 等を `.rb` が呼ぶと、dev ビルドでは一度だけ警告が出ます（本番は無音 no-op）。

### ライター向け一覧: Novel > Project Reference

使える名前（キャラ・画像・BGM/SE）と構図を種別ごとのタブで一覧するエディタウィンドウです。キャラカタログと
Resources の画像キーはアセットから常時表示され、音キーと構図は **一度再生したときの実際の DI 配線からキャプチャ**
されて表示されます（game 側の登録作業は不要）。
画像・立ち絵はサムネイル付きで、行クリックで Project 内のアセットを ping します。各行の **コピー** ボタンで
キーをクリップボードへ入れられます（コピーされるのは常に、シナリオにそのまま書ける実キーです）。音キーは ▶ ボタンで
その場で試聴できます。試聴対象のクリップは、`EnumerateKeys()` が `AudioKeyInfo` の `asset` 引数に AudioClip を
渡していればそれを使い（推奨。キー体系に依存せず GUID で永続化）、無ければキーを Resources 相対パスとして
照合します。どちらでも特定できないキーは試聴ボタンが無効になります。
キャラタブは、キャラごとにその子の立ち絵キーをすべて並べます。どの立ち絵がどのキャラのものかは
「既定立ち絵と同じフォルダ」→「パスの途中にキャラ id がある」→「ファイル名が `<id>_` で始まる」の順で
推定します。キャラを特定する部分を落とした短縮表記も併記しますが、これは読みやすさのための表示であり、
シナリオに書くのは実キーの方です。

画像キーは「シナリオにそのまま書ける文字列」として表示されます。`ISpriteLoader` が root を付ける構成
（`new ResourcesSpriteLoader("Novel/")` など）では、その root を差し引いたキーが出て、root の外にある
スプライトは「このシナリオからは読めない」と明示されます。この補正が効くのはローダが
`ISpriteKeyPrefix` を実装している場合だけです（`ResourcesSpriteLoader` は実装済み）。自前ローダで
root 相当の加工をしているなら、次のように名乗ってください。

```csharp
public sealed class MySpriteLoader : ISpriteLoader, ISpriteKeyPrefix
{
    public string KeyPrefix => "Art/Novel/";
    // ...
}
```

名乗らないローダの場合は Resources 相対パスがそのまま表示され、ウィンドウ上部にその旨の注意が出ます。

自前実装を一覧に載せるには `IAudioChannel.EnumerateKeys()` / `ICharacterCatalog.EnumerateEntries()` で
目録を返してください。この 2 つは default 実装を持ちません（実装忘れが「再生しても一覧に出ない」という
沈黙の空目録になるため明示実装が必須です。一覧を持てない実装は空を返します）。構図は
`IPortraitChannel.EnumerateLayouts()` をオーバーライドします（default は標準 5 構図）。
列挙はキーの一覧を返すだけの軽量な実装にし、アセットの実ロードを伴わないようにします。
キャプチャは種別ごとに保持・更新されます。タイトル画面など novel を配線しないシーンのビルドが走っても、
空の列挙は「未提供」とみなされ、キャプチャ済みの音キー・キャラ・構図は消えません。

`NovelMessageView`（参考 View）は TMP のメッセージ窓・選択肢ボタンを serialized 参照で持ちます。
送り入力（クリック/決定）は `view.Advance()` を呼んで進めます（入力方式に依存しないため）。

完成形の例は **Basic サンプル**（`Samples~/Basic`）を参照。UPM パッケージとして導入した場合は
Package Manager の novel-kit → *Samples* → **Basic** を Import すると `Assets/Samples/` に展開されます
（リポジトリ上は [`Assets/Novel/Samples~/Basic`](../Assets/Novel/Samples~/Basic)）。

---

## 4. 再生する

ランナーを注入で受け取り、シナリオキーを渡して呼ぶだけです。進行（どのシナリオをいつ出すか）は
game 側の責務で、ライブラリは「命令された 1 シナリオを完了まで再生する」プリミティブに徹します。

```csharp
public sealed class NovelStarter : IStartable
{
    readonly INovelScenarioRunner _runner;
    public NovelStarter(INovelScenarioRunner runner) => _runner = runner;

    public async void Start()
    {
        NovelResult result = await _runner.PlayAsync("intro", default);
        // result: Completed / Cancelled / Faulted
        // Faulted は「シナリオ内で例外が起きた」か「シナリオが見つからなかった」。
        // 詳細（Ruby backtrace・落ちた時点の say 通番）は INovelErrorHandler へ届く
        // 分岐に必要な結果はフラグとして state に残るので、game はそれを読んで次を決める
    }
}
```

---

## 4.4. CinematicEffect の演出をシナリオから呼ぶ（任意）

[cinematic-effect](https://github.com/void2610/cinematic-effect) を導入していれば、演出アセットを置くだけで
シナリオから呼べます。**コードも対応表も要りません。**

1. `builder.RegisterNovelCinematicEffects();` を `RegisterNovelKit()` / `RegisterNovelKitCore()` の後に足す
2. `Create > Cinematic > Sequence Asset` で演出を組み、`Resources/Novel/Effects/<key>.asset` に置く
3. シナリオで `cinematic :key` / `cinematic_stop :key` と書く

キーはアセット名（`Resources/Novel/Effects/` からの相対パス・拡張子なし）です。`cinematic_stop :key` は
`<key>_exit.asset` を再生します。止め方 (どのエフェクトをどう戻すか) も演出の一部なので、Enter と同じく
プロジェクトがアセットで決めます。ライブラリが Enter の中身から止め方を推測することはしません。
一回で終わる演出（自分で `Stop` まで持つもの）に `_exit` は不要です。
使えるキーは `Novel > Project Reference` の「演出」タブに並び、`Novel > Validate Scenarios` が
未定義キーを警告します。

`CinematicEffectDirector` はシーンにあればそれを使い、無ければ自動生成します。`shake` / `flash` /
`fade_out` / `fade_in` / `blackout` の標準 5 種も同時に `IWorldEffectSink` として登録されるので、
これらは即座に動きます。ゲーム固有の `world_effect` キーを持つ場合は自前の sink を後勝ち登録し、標準 5 種は
`BuiltinTransitionWorldEffectSink.TryBuild()` で組んで委譲してください。

> Addressables 等に載せたい場合は `ICinematicSequenceLoader` を後勝ち登録します。ただしエディタの一覧・検証は
> `Resources/Novel/Effects/` の規約で走査するため、その場合は一覧に出ません。

## 4.5. エラーの受け取り方

既定では `DebugNovelErrorHandler` が登録され、シナリオ内の例外を `Debug.LogError` に出します
（`RegisterNovelKitCore` / `RegisterNovelKit` のどちらでも同じ）。黙らせたい場合だけ
`NullErrorHandler` を後勝ち登録してください。

例外にならない不具合（シナリオが見つからない・画像キーが解決できない等）は、dev ビルドでは
ライブラリが `Debug.LogWarning` を出します。ゲーム独自のオーバーレイに出したい場合は
`OnRuntimeIssue` を実装します（default 実装があるので、必要なときだけ書けば済みます）。

```csharp
public sealed class MyErrorHandler : INovelErrorHandler
{
    public void OnScenarioFaulted(NovelErrorInfo error)
        => ShowOverlay($"{error.Message}\n{error.Detail}（直前のセリフ: {error.LastSayText}）");

    public void OnRuntimeIssue(NovelIssueInfo issue)
        => ShowToast(issue.Message);   // Kind で ScenarioNotFound / SpriteNotFound 等を判別できる
}
```

> `.rb` の行番号はエラーに出せません。バイトコードにデバッグ情報が含まれないためで、代わりに
> `NovelErrorInfo.SayNumber` / `LastSayText`（落ちる直前に処理した say の通し番号と原文）が
> 位置の手掛かりになります。原文はタグ込み・未 resolve で渡るため、そのまま `.rb` を検索できます。

---

## 5. 自前 View に差し替える

参考 View を使わず独自 UI にする場合は `INovelView` を実装して登録するだけです。

```csharp
public interface INovelView
{
    UniTask      ShowMessageAsync(NovelLine line, CancellationToken ct);
    UniTask<int> ShowChoicesAsync(IReadOnlyList<string> options, CancellationToken ct);
}
```

`ShowMessageAsync` が `await` している間だけ MRuby の進行が止まる（＝「表示→送り待ち→次」）ので、
タイプライタや送り待ちは View の `await` で表現します。

タイプライタの進行ロジック（逐次 Reveal・速度/区間制御・`<w>`/`<p>`/`<speed>` 解釈・auto/skip 待ち）は
Runtime の `TextRevealEngine` に実装済みです。自前 View はこれを**駆動するだけ**でよく、進行ロジックを
再実装する必要はありません（描画 API への依存だけを書きます）。

```csharp
// 自前 View 側
readonly TextRevealEngine _engine = new(settings, new MyFrameClock());   // IFrameClock は自前の dt/yield を供給

async UniTask INovelView.ShowMessageAsync(NovelLine line, CancellationToken ct)
{
    var tokens = NovelTagLexer.Parse(line.Text);
    _engine.Build(tokens);                                  // 制御列・shake/wave 区間・総可視文字数を構築
    // tokens から自分の描画バックエンド用の文字列を組み立てる（TMP 参考実装は <noparse> で素テキストを包む）
    await _engine.RevealAsync(line.IsAlreadyRead,
        visible => SetVisibleCharacterCount(visible), ct); // 可視文字数の反映だけ書く
}
```

送り入力・auto/skip は `_engine.RequestAdvance()` / `_engine.Auto` / `_engine.Skip` に流します。
進行は `IFrameClock`（`DeltaTime` と次フレーム待ち）で抽象化されているため、fake clock を渡せば
進行ロジックをヘッドレスにテストできます（`TextRevealEngineTests` 参照）。

---

## 6. 多言語対応（opt-in）

日本語のみなら何も要りません（既定の `IdentityTextResolver` が原文をそのまま表示します）。
多言語化するときは Unity Localization Package（`com.unity.localization`）を導入すると
`Novel.Localization` / `Novel.Localization.Editor` アセンブリが有効になります。
`.rb` は日本語直書きのまま変わらず、**原文そのものをキー**に String Table から訳を引きます
（未ヒットは原文フォールバック）。

1. `Window > Asset Management > Localization Tables` で String Table Collection（例 `NovelText`）と
   ロケールを作成する。
2. `Novel/Localization/Extract Strings...`（エディタ抽出ツールは後続 PR で提供予定）で `.rb` から
   原文を抽出してテーブルへ登録する
   （2 回目以降は差分抽出: 原文の誤字修正やタグ変更は訳を保持したまま追従し、リネーム内容は
   適用前にレポートで確認できる。消えた原文も訳は削除されない）。
3. resolver を後勝ち登録で差し替え、再生前に初期化する。

```csharp
// LifetimeScope（RegisterNovelKitCore/RegisterNovelKit の後に登録して上書き）
builder.Register<ITextResolver>(
    _ => new LocalizedTableTextResolver("NovelText", sourceLocaleCode: "ja"), Lifetime.Singleton);

// 再生前に一度 await（テーブルの preload。ロケール切替後も確実を期すなら再度呼ぶ）
await resolver.InitializeAsync(ct);
```

- **`.rb` に出ないテキストの扱い**:
  - **キャラの表示名**（カタログ）は抽出対象です。`ScriptableCharacterCatalog` アセットと
    DI ビルド時キャプチャ（コード実装カタログ）の和集合から集め、シナリオ本文と同じ追跡に乗せます
    （キャラを改名すると当てた訳が追従します）。表示名が空 / id と同じものは対象外です。
  - **`guest: true` の話者名**は `.rb` 内にあるので抽出されます（未登録 id がそのまま表示名になるため）。
  - **`ITextVariableProvider` が返す値**（主人公名など）はゲーム側が供給する文字列なので、
    翻訳が必要ならゲーム側で言語別の値を返してください（ライブラリは展開するだけです）。
  - **ふりがな辞書**（`IRubyDictionary`）は日本語専用の表示装飾です。非 JP ロケールでは
    空辞書に差し替えて無効化してください。
  - ゲーム UI（セーブ画面やボタン等）はライブラリの管轄外です。
- 既読/スキップは原文基準なので、言語を切り替えても既読状態は保たれます。
- ロケール切替は次に表示される行から反映されます（表示中の行・バックログは遡って変わりません）。
- 訳テーブルは選択中ロケールのものだけを引き、Unity Localization のフォールバックロケール連鎖
  （pt-BR→pt 等）には現状対応していません。地域バリアントロケールを使う場合はそのロケール自身の
  テーブルを用意してください（`sourceLocaleCode` の原文判定は地域バリアントも一致扱いです）。
- 文中の変数は `%{gold}` 形式（テキスト変数）で書きます。テンプレートのまま翻訳テーブルのキーになり、
  訳の取得後に値が差し込まれるため、翻訳者は訳文中でプレースホルダを自由に動かせます。値は既定で
  `IStateStore`（`flag`/`val`）から取り、主人公名などゲーム固有値は `ITextVariableProvider` の
  後勝ち登録で供給します。Ruby の `#{}` 補間は焼き込みになるためローカライズできません
  （[シナリオライターズガイド 7 章](./scenario/07-pitfalls.md)）。
- 糖衣の間接呼び等で静的抽出から漏れた行は、dev プレイで
  `resolver.TextMissed += MissingTextCollector.Record;` と配線し `MissingTextCollector.Snapshot()` で
  回収できます（エディタメニュー `Novel/Localization/Report Missing Texts` は後続 PR で提供予定）。
  配線しない場合も、訳の付かない原文があると dev ビルドで一度だけ警告が出ます。

---

## さらに詳しく

差し替え口・語彙の拡張・opt-in アセンブリの作法は [`Docs/extending.md`](./extending.md) にまとめています。
シナリオ記述（DSL）のリファレンスは [`Docs/scenario/`](./scenario/index.md)（シナリオライターズガイド）を参照。
設計の意図・意思決定の理由は [`Knowledge~/design/`](../Knowledge~/design/index.md)（OKF 知識ベース）に
記録しています。公開 API の集約は [`Knowledge~/design/api-surface.md`](../Knowledge~/design/api-surface.md) を参照。
