# novel-kit の拡張点 — 差し替え口・語彙・opt-in アセンブリ

エンジニア向け。novel-kit を「差し替える」「語彙を足す」「任意依存の機能を opt-in で足す」ときに、
どこに何を差すかと、その作法をまとめる。入門は [`getting-started.md`](./getting-started.md)。

拡張は 3 つの層に分かれる。

| 層 | 何を足すか | 差し込み方 |
|---|---|---|
| DI の差し替え口 | ローダ・表示ファセット・テキスト解決・診断 | `RegisterNovelKit()` / `RegisterNovelKitCore()` の**後**に同じ interface を登録する (後勝ち) |
| 語彙 | シナリオから呼べる新しい命令 | `INovelCommandModule` + 糖衣の preamble |
| エディタ | Project Reference のタブ・Validate Scenarios の検証 | `IProjectReferenceSection` / `IScenarioKeyExtension` を `[InitializeOnLoad]` で登録 |

任意依存 (Addressables / Unity Localization / cinematic-effect) の機能は、この 3 層を **opt-in アセンブリ**に
まとめて提供する。作法は末尾の「opt-in アセンブリの作り方」。

---

## 1. DI の差し替え口

`RegisterNovelKitCore()` は全ての口に **無音の既定** (Null 実装) を登録し、`RegisterNovelKit()` はその上に
Resources ローダと dev 警告付きファセットを重ねる。ゲームはそのさらに後に登録すれば差し替わる (VContainer の後勝ち)。

### ロード系

| interface | 既定 (`RegisterNovelKit`) | 差し替えの典型 | 備考 |
|---|---|---|---|
| `IScenarioSource` | `ScenarioSource(ResourcesTextAssetLoader, "Scenarios/")` | 自前 (CR は `CrScenarioSource`) | `RegisterNovelKitCore` 派は**必須登録** |
| `IPreambleSource` | 組込 `Novel/Preamble` | **追加**登録 (糖衣の `.rb`) | 複数登録可。**登録順に評価**され、後のものが先の定義を上書きできる。opt-in 機能も自分の preamble をここに足す |
| `ITextAssetLoader` | `ResourcesTextAssetLoader` | `AddressablesTextAssetLoader` (opt-in) | `.rb` → `.mrb` サブアセットの取り出し口。`ScenarioSource` / `PreambleSource` に渡す |
| `ISpriteLoader` | `ResourcesSpriteLoader()` (root なし) | `ResourcesSpriteLoader("Novel/")` / `AddressablesSpriteLoader` (opt-in) | root を付けるなら **`ISpriteKeyPrefix` も実装**する。エディタ (Project Reference / Validate) が実キーを出せるのはこれを名乗るローダだけ |
| `ICinematicSequenceLoader` | `ResourcesCinematicSequenceLoader` (opt-in) | Addressables 等 | root は規約 `Resources/Novel/Effects/` で**固定**。差し替えるとエディタの一覧・検証には出ない (規約フォルダを走査するため) |
| `IRubyDictionary` | `Resources/Novel/ruby.rb` (`RubyDictionary.DefaultKey`) を読む `RubyDictionary` | 自前 | Core 既定は `NullRubyDictionary` |

### 表示ファセット (シナリオの命令が作用する先)

| interface | 命令 | 既定 | 備考 |
|---|---|---|---|
| `INovelView` | say / choose / ウィンドウ | **なし (必須)** | 参考実装 `NovelMessageView` (TMP) |
| `ICharacterCatalog` | 話者 id → 表示名・既定立ち絵 | **なし (必須)** | `EnumerateEntries()` は default 実装なし (明示実装必須) |
| `IPortraitChannel` | portrait / stage | dev 警告の no-op | `EnumerateLayouts()` は default = 標準 5 構図。`IStageLayoutEditor` を併せて実装すると Stage Preview で座標編集できる |
| `IPortraitDirector` | 立ち絵の slot 解決 | `DefaultPortraitDirector` (内部で `IPortraitChannel` を使う) | 通常は差し替えない |
| `IBackgroundChannel` / `IStillChannel` / `ICenterImageChannel` | bg / still / image | dev 警告の no-op | 解決済み `ResolvedSprite` が渡る (ロードは runner) |
| `IAudioChannel` | se / se_loop / bgm | dev 警告の no-op | `EnumerateKeys()` は default 実装なし (明示実装必須)。`AudioKeyInfo.Asset` に AudioClip を渡すと Project Reference で試聴できる |
| `IWorldEffectSink` | world_effect (shake 等) | Core: 無音 / CinematicEffect opt-in: 標準 5 種 | 返す `UniTask` の完了で blocking 性が決まる (effect-await ADR)。`EnumerateKeys()` は default 実装なし (明示実装必須。標準 5 種は `BuiltinTransitionWorldEffectSink.BuiltinKeys()` を連結) |

### テキスト・診断

| interface | 既定 | 備考 |
|---|---|---|
| `ITextResolver` | `IdentityTextResolver` | 多言語は `LocalizedTableTextResolver` (opt-in) を後勝ち登録 |
| `ITextVariableProvider` | `IStateStore` の変数値へフォールバック | `%{name}` 等の game 固有値 |
| `INovelErrorHandler` | `DebugNovelErrorHandler` (LogError) | 黙らせるなら `NullErrorHandler`。`OnRuntimeIssue` は default 実装付き (実装すれば独自オーバーレイに出せる) |
| `IBacklog` | `RingBufferBacklog(200)` | 容量を変えるならファクトリ登録 |
| `INovelPlaybackSettings` | `DefaultNovelPlaybackSettings` | 文字/秒・auto の行末待ち・未読スキップ可否 |

### 列挙の契約 (Project Reference / Validate Scenarios の情報源)

エディタは「アセットから静的に読めるもの」は直接走査し、「実行時にしか実体がないもの」は
**DI ビルド時にキャプチャ**する (`RegisterNovelKitCore` の build callback → `Library/NovelKit` に永続化)。

| 情報源 | 取り方 | 差し替え側の責務 |
|---|---|---|
| キャラ | `ScriptableCharacterCatalog` アセット ∪ `ICharacterCatalog.EnumerateEntries()` | コード実装のカタログは `EnumerateEntries` を返す |
| 画像キー | `Resources` の Sprite 走査 | root 付きローダは `ISpriteKeyPrefix.KeyPrefix` を名乗る |
| 音キー | `IAudioChannel.EnumerateKeys()` | 明示実装必須 (一覧を持てないなら空を返す) |
| 構図 | `IPortraitChannel.EnumerateLayouts()` | 独自構図があればオーバーライド |
| 演出キー (opt-in) | `Resources/Novel/Effects/` の走査 | なし (規約に従って置く) |
| 独自コマンド | `INovelCommandModule.RegisterVocabulary` を記録用 vocabulary で呼ぶ | `[NovelDescription]` で説明を付ける |
| 糖衣 | 再生 1 回目の preamble ロード時に定義されたメソッドとバイトコードのハッシュ | `.rb` を Resources 等のアセットとして持てば、エディタがハッシュで特定して引数名・既定値・直上コメントを出す |
| world_effect キー | `IWorldEffectSink.EnumerateKeys()` | 明示実装必須 |

列挙はキーの一覧を返すだけの軽量な実装にし、アセットの実ロードを伴わないこと。
「空 = 列挙未提供」とみなして以前のキャプチャを保持するため、`EnumerateKeys` が一時的に空を返しても一覧は消えない。

---

## 2. 語彙を足す (`INovelCommandModule`)

新しい命令は「コマンド型 + 語彙登録 + ハンドラ」を 1 クラスに同居させて差し込む。
書き方は [`getting-started.md` の「プロジェクト独自コマンドを足す」](./getting-started.md#プロジェクト独自コマンドを足す)。

```csharp
builder.RegisterNovelCommand<MyCommands>();   // runner が IEnumerable<INovelCommandModule> として集約注入する
```

登録した語彙は Project Reference の「コマンド」タブに並ぶ (DI ビルド時に記録用の `INovelVocabulary` で読む)。
コマンド型と各プロパティに `[NovelDescription("…")]` を付けると、説明がタブに出る。
糖衣 (preamble の `def`) も同じタブに並び、`.rb` の `def` 直上のコメントがそのまま説明になる。

モジュールのコンストラクタで受け取れる、novel-kit 側の共有物:

| 型 | 用途 |
|---|---|
| `NovelPlaybackProgress` | `IsFastForwarding` (セーブ復帰の早送り中か)・`ScenarioKey` (再生中のシナリオ)・`SayNumber` / `LastSayText`。**演出系は早送り中は再現しない**のが組込の流儀 (world_effect / cinematic と同じ) |
| `INovelErrorHandler` | 例外にならない不具合の通知先。`NovelDiagnostics.Report(handler, issue)` を通すと dev ログとハンドラ通知が揃う |
| 表示ファセット・ローダ | 既定登録済みなので普通に注入できる |

### 糖衣 (Ruby 側の関数) の足し方

`cmd :my_cmd, power: 2.0` は語彙登録だけで書けるが、`my_cmd 2.0` と書かせたいなら糖衣が要る。
MRubyCS は実行時に Ruby ソースを eval できないため、糖衣は **`.rb` を Resources に置き (インポータが `.mrb` に
コンパイルする)、`IPreambleSource` として追加登録**する。

```csharp
builder.RegisterInstance<IPreambleSource>(new PreambleSource(new ResourcesTextAssetLoader(), "Scenarios/MyPreamble"));
```

### 糖衣の自動生成 (`RegisterNovelCommandSugars`)

引数をそのまま渡すだけの糖衣なら、手書きせず語彙から自動生成できる。

```csharp
builder.RegisterNovelKit();
builder.RegisterNovelCommandSugars();   // ← RegisterNovelKit の後・自前 preamble 登録の前
builder.RegisterNovelCommand<MyCommands>();
```

- エディタの DI ビルド時 (再生開始時) に語彙をキャプチャし、`Assets/Resources/Novel/CommandSugars.rb` を生成する
  (内容が変わったときだけ上書き)。生成物は git にコミットする。**新しいコマンドの糖衣が効くのは次の再生から**
- 生成形は `def screen_shake(power = nil, duration = nil, **kw)` — 宣言順の位置引数とキーワードの両方で書け、
  渡した引数だけが `cmd` に渡る (未指定は C# 側の既定値)。`[NovelDescription]` が def 直上コメント =
  Project Reference の説明になる
- 組込語彙・組込 preamble と同名のコマンドは生成せず警告する。**手書き糖衣が常に後勝ち**なので、
  引数変換など凝った糖衣にしたいコマンドは自前の preamble に同名 def を書けばそちらが有効になる
  (呼び出し順の前提: 上記の登録順を守ること。preamble は登録順に評価される)

### Validate Scenarios との関係 (落とし穴)

Validate Scenarios はシナリオを**スタブ実行**してキーを集める。組込語彙しか知らないので、独自コマンドは
「未登録コマンド」として no-op に置き換えられ、**そのコマンドが使うキーは検証されない** (警告に名前が出る)。
検証させたいなら次節の `IScenarioKeyExtension` で語彙・記録モジュール・**preamble**・正解集合を渡す。
preamble を渡し忘れると糖衣が未定義のまま stub 化され、キーが 1 件も記録されない (CinematicEffect 実装時にテストで発覚)。

---

## 3. エディタの拡張点

コアの `Novel.Editor` は opt-in アセンブリを参照しない (asmdef の任意参照が扱いづらい)。依存の向きは
**opt-in → コア**で、opt-in 側が `[InitializeOnLoad]` の静的コンストラクタで登録する。

| 拡張点 | 何ができるか | 実装例 |
|---|---|---|
| `IProjectReferenceSection` | Project Reference に組込タブの後ろへタブを足す。`Rows` 経由で組込と同じ見た目の行 (サムネ・キーチップ・コピー・ping) を描ける。`Invalidate()` で「更新」に追従する | `Novel.CinematicEffect.Editor` の「演出」タブ |
| `IScenarioKeyExtension` | Validate Scenarios に語彙を教える。`CreateRecorder(keys)` で語彙登録 + 記録するモジュール、`PreambleSources()` で糖衣、`ScanKnownKeys()` で正解集合 (null = 情報源なし → スキップ) | 同上の `cinematic` キー検証 |

```csharp
[InitializeOnLoad]
internal static class MyFeatureReference
{
    static MyFeatureReference()
    {
        ProjectReferenceSections.Register(new MySection());
        ScenarioKeyExtensions.Register(new MyKeyExtension());
    }
}
```

「正解集合が 0 件なら null を返す」のは組込と同じ割り切り (空集合として検証すると全キーが未定義の大量誤警告になる。
誤検知より見逃しに倒す)。

---

## 4. opt-in アセンブリの作り方

任意依存の機能は、その依存パッケージが manifest にあるときだけコンパイルされるアセンブリにする。
既存の 3 つが雛形。

| 機能 | 依存パッケージ | define | アセンブリ |
|---|---|---|---|
| Addressables ローダ | `com.unity.addressables` | `NOVEL_ADDRESSABLES` | `Novel.Addressables` |
| 多言語 | `com.unity.localization` | `NOVEL_LOCALIZATION` | `Novel.Localization` / `.Editor` |
| CinematicEffect | `com.void2610.cinematic-effect` | `NOVEL_CINEMATIC_EFFECT` | `Novel.CinematicEffect` / `.VContainer` / `.Editor` |

### asmdef

`versionDefines` で依存パッケージの存在を define に変え、同じ define を `defineConstraints` に置く。
これでパッケージが無ければアセンブリごとコンパイル対象から外れる。

```json
"defineConstraints": ["NOVEL_MYFEATURE"],
"versionDefines": [{ "name": "com.example.dependency", "expression": "", "define": "NOVEL_MYFEATURE" }]
```

### 分割

| アセンブリ | 中身 | 参照 |
|---|---|---|
| `Novel.<Feature>` | ローダ実装・コマンドモジュール・純粋ロジック・preamble `.rb` | `Novel.Runtime` + 依存パッケージ (VContainer には依存しない) |
| `Novel.<Feature>.VContainer` | 登録ヘルパ `Register<Feature>()` | 上 + `Novel.VContainer` (+ `Novel.View` の Resources ローダを使うなら) |
| `Novel.<Feature>.Editor` | Project Reference / Validate への差し込み | 上 + `Novel.Editor`。`includePlatforms: ["Editor"]`、`autoReferenced: false` |

ローダだけの機能 (Addressables) は 1 アセンブリで足りる。

### 登録は明示 1 行

`RegisterNovelKit()` がパッケージの存在で自動配線する形は採らない (コア側から opt-in アセンブリを参照する
ことになり、asmdef の任意参照が扱いづらい)。ゲームは `builder.Register<Feature>();` を 1 行足す。

### テスト

テスト asmdef (`Novel.Tests.EditMode`) に同じ `versionDefines` を足し、アセンブリ参照を追加して、
テストファイル全体を `#if NOVEL_MYFEATURE ... #endif` で囲む。パッケージが無い環境でもテストアセンブリが壊れない。

テストは**テストが要るロジックだけ**に絞る (純粋ロジック・実際に欠陥を捕まえうる契約)。テストしやすさのために
本番コードへ interface や分離を足さない。MonoBehaviour 依存の配管 (ロードして再生する等) は未テストで構わない。

### 本リポジトリでの開発

opt-in 機能を novel-kit 自身で開発・テストするには、依存パッケージを `Packages/manifest.json` に入れる
(CinematicEffect は LitMotion も要る)。利用側プロジェクトの manifest に入れるのはその機能を使うときだけ。
