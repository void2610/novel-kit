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
bg "room"
portrait :alice, "smile"
say "alice", "やあ、ようこそ <color=#8cf>novel-kit</color> へ。"
say "alice", "文字は<w=0.4>少しずつ<shake>出る</shake>よ。"
narration "——彼女はこちらを見た。"
n = choose(["はい", "いいえ"])
flag "answered", 1
if n == 0
  say "alice", "うれしい！"
else
  say "alice", "そっか……。"
end
```

### 使える命令（preamble の糖衣）

| 命令 | 意味 |
|---|---|
| `say(speaker, text, display_as: nil)` | セリフ。`speaker` 省略相当（空文字）でナレーション |
| `narration(text)` | ナレーション（話者なし） |
| `choose(["A","B"])` | 選択肢を出し、選んだ index（0 始まり）を返す |
| `flag(key, value=1)` | フラグ/変数を設定（`state[:key]` で読める） |
| `portrait(char, key)` | 立ち絵（単一スロット差し替え） |
| `bg(key)` / `still(key)` | 背景 / 一枚絵 |
| `se(key)` / `bgm(key)` | 効果音 / BGM（`bgm("")` で停止） |
| `wait(seconds)` | 明示待機 |

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
テキスト解決（恒等）・各 View ファセットの no-op 既定。`INovelView` と `ICharacterCatalog` だけは game が登録します。

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
タイプライタや送り待ちは View の `await` で表現します。本文のインラインタグ解析は
`NovelTagLexer.Parse(line.Text)` でトークン列を得て処理できます。

---

## さらに詳しく

設計の意図・意思決定の理由は [`Knowledge~/design/`](../Knowledge~/design/index.md)（OKF 知識ベース）に
記録しています。公開 API の集約は [`Knowledge~/design/api-surface.md`](../Knowledge~/design/api-surface.md) を参照。
