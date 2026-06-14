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
| `<color>` `<size>` `<b>` `<link>` など | TMP スタイル（そのまま反映） |
| `<noparse>…</noparse>` | リテラル表示 |

話者 `:alice` 等の表示名は `ICharacterCatalog` で解決します（未登録なら id をそのまま表示）。

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

> **ファセット/サービスの差し替えは `RegisterNovelKit()` の後に登録**してください（後勝ち）。立ち絵・背景・音声・
> セーブ・世界エフェクトを供給するときは `builder.Register<IPortraitView, MyPortraitView>(...)` 等を後に書きます。
> 未供給のまま `portrait`/`bg`/`se` 等を `.rb` が呼ぶと、dev ビルドでは一度だけ警告が出ます（本番は無音 no-op）。

`NovelMessageView`（参考 View）は TMP のメッセージ窓・選択肢ボタンを serialized 参照で持ちます。
送り入力（クリック/決定）は `view.Advance()` を呼んで進めます（入力方式に依存しないため）。

完成形の例は [`Assets/Novel/Samples/`](../Assets/Novel/Samples) のサンプルシーンを参照してください。

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
        // 分岐に必要な結果はフラグとして state に残るので、game はそれを読んで次を決める
    }
}
```

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

## さらに詳しく

設計の意図・意思決定の理由は [`Knowledge~/design/`](../Knowledge~/design/index.md)（OKF 知識ベース）に
記録しています。公開 API の集約は [`Knowledge~/design/api-surface.md`](../Knowledge~/design/api-surface.md) を参照。
