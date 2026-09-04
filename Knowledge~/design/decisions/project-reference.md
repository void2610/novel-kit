---
type: Decision
title: プロジェクトリファレンス — キー列挙はチャンネル契約に統合・実体は DI ビルド時にキャプチャ
description: ライター向けに「使える名前と構図」を一覧するエディタウィンドウを追加する。列挙の契約は IAudioChannel / IPortraitChannel / ICharacterCatalog 自身に統合し（音とキャラは明示実装必須・構図のみ default = 標準 5 構図）、実行時にしか実体がない情報は RegisterNovelKitCore が DI ビルド時にキャプチャしてエディタ側キャッシュへ渡す（game 側の追加記述ゼロ・種別ごとにマージ）。音の参考実装は追加しない。
tags: [decision, editor, tooling, audio, portrait, layout, catalog, writer]
timestamp: 2026-09-05T04:00:00Z
status: 確定
---

# 状況

シナリオライターズガイド（`Docs/scenario/`、2026-08-06 整備）を書いた結果、ガイドが
「プロジェクトの資料で確認」「エンジニアに確認」へ逃げている箇所が 13 箇所あることが分かった。
内訳は (1) 使える名前の一覧（キャラ id・立ち絵/背景/一枚絵/補足画像キー・SE/BGM キー）、
(2) 構図（レイアウト）の一覧、(3) 独自命令、(4) 配線状況。

情報源はすべて Unity アセット・C# コード・DI 登録に散っており非プログラマには読めない。
かつ novel-kit は未配線・キー間違いを no-op で流す設計（[DSL 語彙](/design/decisions/dsl-vocabulary.md)）
のため、書き間違いが沈黙し、一覧なしでは通し確認頼みになる。手書き資料は必ず陳腐化する。

ユーザーとの議論（2026-08-06）で、主目的を **名前と構図の確認**に絞り、
最初からエディタウィンドウとして実装する（Markdown 書き出しはしない）ことで合意した。

# 決定

## 1. エディタウィンドウでの一覧（v1 スコープ = 名前 + 構図）

`Novel.Editor` にプロジェクトリファレンスウィンドウ（メニュー名は実装時確定。例: Novel > Project Reference）
を追加し、以下を一覧する。

| セクション | 情報源 |
|---|---|
| キャラ（id・表示名・既定立ち絵） | `ScriptableCharacterCatalog` アセット検索（既存資産） |
| 画像キー（立ち絵/背景/一枚絵/補足画像） | Resources 配下のスプライトスキャン（キー = Resources 相対パスの規約に基づく） |
| 構図（レイアウト id・スロット数） | `IPortraitChannel.EnumerateLayouts()`（下記 2） |
| SE / BGM キー | `IAudioChannel.EnumerateKeys()`（下記 2） |

Markdown 等へのエクスポートは v1 では持たない。独自命令一覧・実行時配線診断はスコープ外。

## 2. 列挙の契約はチャンネル自身に統合する

「キーを解決して再生する実装が、キーの一覧も知っている」のは自明なので、
別 interface（provider）を新設せず **既存ファセットに列挙メンバーを統合**する。

```csharp
// Novel.Runtime
public interface IAudioChannel
{
    // …既存の再生 API…
    IEnumerable<AudioKeyInfo> EnumerateKeys() => Array.Empty<AudioKeyInfo>();   // default 実装
}

// Novel.Assets
public interface IPortraitChannel
{
    // …既存 API…
    IEnumerable<StageLayoutInfo> EnumerateLayouts() => StageLayoutInfo.Defaults; // 標準 5 構図
}
```

- default interface method により **既存実装（WarningNullFacets・各ゲームのチャンネル）は無改修**で
  コンパイルが通る。列挙に参加したい実装だけオーバーライドする。
- `AudioKeyInfo` は key / 種別（BGM/SE）/ ライター向けメモ（任意）、`StageLayoutInfo` は
  layout id / スロット数 / メモ（任意）を持つ軽量構造体。

## 3. 実行時にしか実体がない情報は DI ビルド時にキャプチャする

エディタウィンドウは編集モードで動くが、チャンネルは実行時 DI でしか実体化しない。
このギャップは編集モード側で埋めない。**実際にコンテナが組み上がる瞬間に novel-kit 自身が吸い上げる**。

`RegisterNovelKitCore()`（game が既に呼んでいる）にエディタ実行時のみ有効な
`RegisterBuildCallback` を仕込み、構築済みコンテナから `IAudioChannel` / `IPortraitChannel` を
解決して列挙結果をエディタ側キャッシュ（`Library/` 配下・ドメインリロード/再起動を跨いで保持）へ書き出す。
ウィンドウはキャッシュを取得時刻つきで表示する。

- **game 側の追加記述はゼロ。** チャンネル実装 + 列挙オーバーライド + いつもの DI 登録だけで
  ウィンドウに載る。チャンネルの実体が plain class か MonoBehaviour か等の入手問題は、
  DI が解決できている時点で存在しない。
- **忠実度が最高。** 後勝ち差し替え・スコープ構成まで含めた「実際に鳴らす配線」から取るため、
  編集モードでの再構築のような本番との乖離が原理的にない。
- トレードオフとしてデータは「最後に再生した時点」のもの。未キャプチャ時はウィンドウが
  「一度再生してください」と案内する。ただしアセットとして静的に読めるもの
  （キャラカタログ・Resources の画像キー）は再生不要でライブ表示し、
  キャッシュ頼みになるのは実行時にしか実体がない部分（音キー・構図）に限る。
- キャプチャは try/catch で保護し、失敗しても game の起動を妨げない（警告ログのみ）。

## 4. 音の参考実装は追加しない

`IAudioChannel` の参考実装（SO カタログ + プレイヤー）は同梱しない（ユーザー判断・2026-08-06）。
対象プロジェクトは音響実装を既に持っており、一覧の情報源としては `EnumerateKeys()` の
オーバーライドで足りる。音は従来どおり interface のみ提供・実装は game 所有のまま。

# 理由

- 列挙をチャンネル契約に統合すると、契約が 1 つで済み、「鳴らせるのに一覧に出ない」という
  実装とカタログの乖離が構造的に起きない。
- DI ビルド時キャプチャは、game が既に書いている配線（interface 実装 + DI 登録）以外の
  記述を一切要求しない。配線の知識を二重に書かせる案（マニフェスト・editor フック）は
  すべて同型の陳腐化リスクを持つため退けた。
- default 実装により後方互換が保たれ、既存 6 プロジェクトのチャンネル実装に影響しない。

# 帰結

- `IAudioChannel` / `IPortraitChannel` にメンバー追加（default 実装付き・非破壊）。
  `AudioKeyInfo` / `StageLayoutInfo` を追加。
- `Novel.VContainer` の `RegisterNovelKitCore()` にエディタ限定のキャプチャコールバックを追加。
  受け口（キャプチャハブ）は `Novel.Runtime` に `#if UNITY_EDITOR` で置き、
  `Novel.Editor` が購読して `Library/` 配下へ永続化 + ウィンドウ表示する。
- game 側の導入作業は「チャンネルの列挙オーバーライド」のみ。
- 列挙契約は Validate Scenarios 突き合わせ（シナリオ中の未定義キー検出）の
  正解データをそのまま兼ねる（下記「実装で確定」参照）。
- `EnumerateKeys()` / `EnumerateLayouts()` は起動負荷を避けるため軽量であること
  （キー列挙のみ。アセットの実ロードを伴わないこと）を契約ドキュメントに明記する。

# 実装で確定

- **Validate Scenarios 突き合わせは次フェーズ候補から昇格して実装**（2026-08-06）。
  `Novel/Validate Scenarios` がコンパイル検証に加え、全 `.rb` が使うキー
  （キャラ/立ち絵/画像/音/構図）を正解データ
  （キャラ = カタログ SO・画像 = Resources スプライト・音/構図 = DI ビルド時キャプチャ）と
  突き合わせて未定義キーを警告する（行番号はソース検索の best-effort 付記）。
- **キーの抽出は正規表現パースではなくスタブ実行**: コンパイル済み `.mrb` を実 preamble 込みで
  早送り実行（`NovelResumePoint.End`。wait 等の実時間を消費しない）し、Router に流れる
  型付きコマンドから記録する。パースの正確さ（コメント/クォート/複数行/`#{}` 補間）を
  mruby 本体に委ね、糖衣の追加にも検証側の追従が不要になる。`say` の第 3 引数立ち絵キーも
  コマンド経由で自然に対象になる。当初の行単位正規表現案は文法知識の二重管理と
  複数行・エスケープでの脆さから実装中に棄却した。
- **choose は回答を変えて選択肢数ぶん再実行**し、単一の回答で到達できる分岐を全て通す。
  複数の choose の組合せでしか到達しない行は対象外（既知の割り切り）。再実行は暴走対策として
  上限 8 回答（超過時は未検証分岐が残る旨を警告して打ち切る）。
- 誤検知より見逃しに倒す: 情報源が無い種別（カタログ 0 個・スプライト 0 枚・未キャプチャ）は
  スキップ、画像キーはローダの root プレフィックスを知らないため後方一致で照合、
  空キー（`bgm ""` = 停止）は対象外。独自コマンドを使うシナリオは語彙が無いため途中で
  止まり、そこまでのキーを検証したうえで「完走しなかった」旨を警告する。
  ライブラリ同梱の preamble / ルビ辞書 `.rb` は実行検証の対象外。

- **ウィンドウ UI は種別ごとのタブ + プレビュー付き**（2026-08-07・ユーザー要望）。
  折りたたみ縦積みからタブ（キャラ / 画像 / 構図 / BGM・SE）へ変更。画像キーと既定立ち絵は
  サムネイル付き・行クリックでアセット ping、構図はスロット配置のミニ図、音キーはエディタ試聴
  （`UnityEditor.AudioUtil` リフレクション・API が無ければ警告して no-op）を提供する
  （一覧の情報源はあくまで列挙契約で、プレビューは追加の便宜）。
- **試聴クリップの一次情報源はアセット参照キャプチャ**（2026-08-08・ユーザー要望）。当初はキーを
  Resources 相対パスとして照合（完全一致 → 後方一致・曖昧なら不解決。ScenarioKeyValidator と同じ
  割り切り）していたが、キー体系が Resources パスと対応しない自前チャンネルでは一切試聴できない。
  `AudioKeyInfo` に任意の `Asset`（チャンネルが保持済みの AudioClip。列挙軽量の契約は不変 —
  このためのロードはしない）を追加し、エディタ側キャッシュが GUID で永続化・読込時に実体へ復元する。
  ウィンドウはこの参照を最優先し、無いキーのみパス照合へ落とす。`Asset` の型は Runtime の
  「signature にアセット型を持ち込まない」方針を守るため `object`（エディタが UnityEngine.Object と解釈）。
- **キャプチャは種別ごとにマージし、Play Mode 由来のみ採用**（2026-08-08・不具合対応）。当初の
  「ビルドごとに丸ごと上書き」は、novel 未配線スコープのビルド（タイトル画面のシーンや EditMode テスト）が
  空のキャプチャで実データを消し、「一度再生しても音・キャラがすぐ消える」不具合になった。
  エディタ側ストアが種別ごとにマージ（空 = 列挙未提供として以前の値を保持。構図は既定実装が標準 5 構図を
  返すため「標準構図のまま = 未提供」の扱い。標準へ意図的に戻すには `Library/NovelKit` を削除して再生し直す）し、
  Edit Mode のコンテナビルドは採用しない。試聴用 AudioClip はドメイン内 Latest のランタイム参照を使い回さず
  常に GUID から実体を引く（再生終了の参照破棄で試聴が死なない）。マージ契約は EditMode テストで固定。
- **音・キャラの列挙に default 実装を置かない**（2026-08-08・ユーザー要望）。`IAudioChannel.EnumerateKeys()` /
  `ICharacterCatalog.EnumerateEntries()` の default（空）は、実装忘れが「再生しても一覧に出ない」という
  沈黙の空目録になるため削除し、明示実装をコンパイルエラーで要求する（当初の「既存実装無改修」より
  発見可能性を優先）。`IPortraitChannel.EnumerateLayouts()` は無指定でも標準 5 構図が実在するため default を維持。
- **スプライトキーの root は ISpriteKeyPrefix でローダに名乗らせる**（2026-08-25・ユーザー要望）。
  当初ウィンドウは Resources 相対パスをそのままキーとして表示していたが、`ResourcesSpriteLoader(root)` を
  使う game では表示キーが実キーとズレ、「一覧のとおりに書いたのに出ない」を招く。`ISpriteLoader` 本体への
  メンバ追加は既存実装を全て壊すため、任意実装のファセット `ISpriteKeyPrefix.KeyPrefix` を切り、
  音キー・構図と同じく DI ビルド時にキャプチャする。ウィンドウは root を差し引いた実キーを表示し、
  root 外のスプライトは「このシナリオからは読めない」と明示する。名乗らないローダでは従来の
  Resources 相対パス表示に落とし、その旨をウィンドウ上部で断る（誤ったキーを断定しない）。
  永続化では「root 不明（未実装）」と「root 空文字（プレフィックス無しが確定）」を区別する必要があるため、
  空判定ではなくローダ型名の有無を presence マーカーにしてマージする。
- **キャラタブは既定立ち絵 1 件でなくキャラごとの全立ち絵を並べる**（2026-08-25・ユーザー要望）。
  ランタイムにキャラ単位のキー名前空間は無い（`portrait :alice, "smile"` はキー `smile` をそのまま読む）ため、
  所在は推定になる。優先順は「既定立ち絵と同じフォルダ」→「パスセグメントがキャラ id と一致」→
  「ファイル名が `<id>_` / `<id>-` で始まる」。カタログが宣言した既定立ち絵は実体が無くても先頭に載せる
  （欠けていること自体が知りたい情報のため）。推定ロジックは `PortraitKeyGrouping` として Unity 非依存の
  純ロジックに切り出し、EditMode テストで固定する。
- **短縮表記は表示専用でコピー対象にしない**（2026-08-25・ユーザー要望）。キャラを特定する部分を落とした
  短縮名（`Characters/aria/smile` → `smile`）を併記して一覧の可読性を上げるが、ランタイムは短縮キーを
  解決しないため、コピーボタンが渡すのは常に実キー。「見やすい表示」と「そのまま貼れる文字列」を
  分けることで、可読性のための加工が誤ったキーの温床にならないようにする。
- **全タブの全行にコピーボタン**（2026-08-25・ユーザー要望）。ping と試聴だけでは結局キーを手で写す必要があり、
  写し間違いが Validate Scenarios 頼みになっていた。画像キー・立ち絵キー・キャラ id・構図（`:single` と
  表示どおりの形）・BGM / SE キーに `EditorGUIUtility.systemCopyBuffer` へ入れるボタンを置く。
  root 外などコピーすべき実キーが無い行はボタンを無効化する。
- **opt-in アセンブリ向けの拡張点** (2026-08-29)。`IProjectReferenceSection` でタブを、
  `IScenarioKeyExtension` で Validate Scenarios の語彙・preamble・正解集合を差し込める。CinematicEffect 連携
  ([ADR](/design/decisions/cinematic-effect.md)) が `[InitializeOnLoad]` で登録する。コアの editor は
  opt-in アセンブリを参照できない (asmdef の任意参照が扱いづらい) ため、依存の向きを逆にした。
- **プロジェクト定義コマンドの一覧** (2026-08-30・ユーザー要望)。`RegisterNovelCommand<TModule>()` の語彙を
  「コマンド」タブに出す。VitalRouter.MRuby の `AddCommand<T>(state, name)` は登録先が非公開クロージャ内の
  `Dictionary` で読み戻せず、リフレクションで潜るのは脆い。そこで語彙の束縛口 `INovelVocabulary` を novel-kit 側で
  持ち (`RegisterVocabulary(MRubyState)` → `RegisterVocabulary(INovelVocabulary)`・破壊的)、runner は MRubyState へ
  委譲、DI ビルド時キャプチャは記録用実装を渡して MRubyState を作らずに Ruby 名 / コマンド型 / モジュール型を読む。
  引数は `[MRubyObject]` 型のプロパティから MRubyCS.Serializer と同じ規則 (snake_case・`[MRubyMember]` 上書き・
  `[MRubyIgnore]` 除外) で導く (属性は名前で見て Serializer への参照を Runtime に持ち込まない)。
  マージは他種別と同じ「空 = 未提供」。
- **糖衣と world_effect キーも同じタブに** (2026-08-30・ユーザー「これで全部？」)。糖衣は MRubyCS に Ruby 側の
  introspection (`methods` / `instance_methods` / `Method#parameters`) が無く、バイトコードにも引数名 (デバッグ情報)
  が無いが、C# 側で `Irep.Symbols` を候補に `TryFindMethod(ObjectClass, …)` の owner が Object かつ Ruby proc のものを
  「ロード前後の解決可否の差」で取れば定義名は分かる (再生 1 回目の preamble ロード時に部分スナップショットとして
  Publish)。引数名・既定値・説明は、バイトコードの SHA-1 で元の `.rb` アセット (ScriptedImporter が `.mrb` を
  サブアセットに持つ) を特定し、ソースの `def` 行と直上コメントを正規表現で読んで補う (`RubyDefParser`・
  入れ子 def は対象外)。ソースが無い環境 (Addressables 等) では名前だけ出す。world_effect キーは
  `IWorldEffectSink.EnumerateKeys()` (default なし・破壊的) で音キーと同じ方式。
- **独自コマンドのコピー雛形は `cmd :name, key: 空値` のキーワード形** (2026-09-05・レビュー指摘)。当初の
  裸呼び形 (`screen_shake 0.0, false`) は動かない: VitalRouter.MRuby が Object に定義する Ruby メソッドは
  `cmd` (と `state`) だけで、`AddCommand<T>` は非公開の VITALROUTER_METHOD_TABLE に足すのみ・MRubyCS は
  method_missing 未対応のため、裸の名前は糖衣が無い限り NoMethodError。`cmd` の実装も
  `GetKeywordArguments()` でキーワード引数しか受けない (上流 MRubyStateExtensions.cs で確認)。

# 検討した代替案

- **手書きマニフェスト SO（宣言一元化）**: 自動で取れない情報を game が SO に転記する案。
  自前音響を持つ game では既存カタログとの二重管理になり陳腐化するため不採用。
- **別 interface（IAudioKeyProvider）+ 型走査で発見**: TypeCache で実装型を列挙し、
  SO はアセット検索・MonoBehaviour はシーン/プレハブ検索・plain class は引数なし生成、と
  入手経路が型ごとに分岐する。暗黙的で説明困難、シーンを開いているかに挙動が依存する、
  発見失敗が沈黙する、と複雑さの割に堅牢でないため不採用。
- **InitializeOnLoad の明示登録（editor フックでファクトリ登録）**: 発見は決定的になるが、
  game が DI に書いた配線と同じ知識を editor 用にもう一度（しかも MonoBehaviour では
  入手経路の選択込みで）書かせる二重記述になるため不採用。DI ビルド時キャプチャが
  同じ情報を追加記述ゼロで得る。
- **Markdown 書き出し先行**: Unity を開かないライターへの配布には有効だが、ユーザー判断で
  最初からエディタウィンドウに一本化。エクスポートは必要になれば後付けできる。
- 2026-09-05 追記: コマンドタブの行を「左: キー名 (太字) / 右: 説明 → コピーされる呼び出し形 → 引数」の 2 カラムへ。
  説明が右端で切れる (LabelField は折り返さない)・コピー文字列が散文と同色で判別できない、という CR での実使用の指摘が起点。
  呼び出し形は monospace + 専用色 + 薄背景のコード行にし、「コピーされる文字列そのもの」を見せる。キー名と同一になる
  呼び出し形 (引数なし糖衣) と、コード行と同内容になるシグネチャ (既定値もキーワードも無い def) は省いてノイズを減らす。
