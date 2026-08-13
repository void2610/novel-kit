#nullable enable
using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using Novel.Runtime;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.Localization.Settings;
using UnityEngine.Localization.Tables;

namespace Novel.Localization
{
    /// <summary>
    /// <see cref="ITextResolver"/> の Unity Localization 実装（原文キー方式・localization-unity-package ADR）。
    /// resolve 前の原文（タグ込み）をキーに指定 String Table を引き、未ヒットは原文をそのまま返す
    /// （テーブル未整備・訳抜けでも従来挙動に落ちる）。
    ///
    /// <see cref="ITextResolver.Resolve"/> は同期のため、game は <see cref="InitializeAsync"/> を
    /// 起動時ないしノベルパート入場前に await してテーブルをロードしておく契約。
    /// 未初期化中の resolve は原文フォールバック（await を忘れた場合は dev ビルドで一度だけ警告する）。
    ///
    /// ロケール切替（SelectedLocale 変更）は自動で追従するが「次の resolve から反映」であり、
    /// 表示中の行・バックログの遡及再描画はしない（ADR）。切替直後〜再ロード完了までは原文フォールバック。
    /// 確実に切り替えてからノベルを再開したい game は、切替後にもう一度 InitializeAsync を await する。
    ///
    /// 制約: テーブルは選択中ロケールのものだけを引き、Unity Localization のフォールバックロケール連鎖
    /// （pt-BR→pt 等）と Smart String 整形は適用しない（訳は素の値・変数展開は %{key} が担う）。
    /// 地域バリアントは <c>sourceLocaleCode</c> の原文判定でのみ一致扱いになる（"ja" と ja-JP）。
    /// ロード済みテーブルは Unity Localization 側のキャッシュ（LocalizedDatabase）に残る。解放（ReleaseTable）は
    /// 他システムとテーブルを共有し得るため resolver は行わず、キャッシュ削除の要否は game が判断する。
    ///
    /// DI: RegisterNovelKitCore() の既定 IdentityTextResolver を後勝ち登録で上書きする。
    /// <code>builder.Register&lt;ITextResolver&gt;(_ =&gt; new LocalizedTableTextResolver("NovelText", "ja"), Lifetime.Singleton);</code>
    /// ファクトリ登録ならスコープ破棄時に VContainer が Dispose する。RegisterInstance で供給する場合は
    /// VContainer が破棄しないため、game が Dispose を呼ぶこと（怠ると静的イベント購読が残りインスタンスがリークする）。
    /// 破棄後の Resolve/InitializeAsync は ObjectDisposedException（NovelScenarioRunner と同じ方針）。
    /// </summary>
    public sealed class LocalizedTableTextResolver : ITextResolver, IDisposable
    {
        private readonly TableReference _tableReference;
        private readonly string? _sourceLocaleCode;
        // ロード済みテーブルの平坦化キャッシュ（原文キー → 訳）。StringTable.GetEntry(string) は共有エントリの
        // 線形走査で、キーが全文行のため再生ホットパスに乗せられない。空値（未翻訳）はミス扱いなので入れない
        private Dictionary<string, string>? _entries;
        private bool _isSourceLocale;
        private bool _disposed;
        private bool _subscribed;
        private bool _initializeRequested;
        private bool _warnedResolveBeforeInit;
        private bool _warnedTextMissedDefault;
        // 再ロードの世代番号。ロケール連打 (en→fr) でロード完了が逆順に届いても、最新要求以外の結果を
        // 捨てて古いロケールのテーブルが居座らないようにする (Unity メインスレッド前提の単純カウンタ)
        private int _reloadGeneration;

        /// <summary>
        /// 訳が付かなかった原文の通知（dev 抽出漏れ収集用）。テーブルロード済み・非原文ロケールでの
        /// ミスのみ発火する（未初期化中や原文ロケールでは発火しない）。購読は任意で、未購読の場合は
        /// dev ビルドで一度だけ既定警告を出す（%{key} ミスの既定警告と対称）。購読者の例外は
        /// 再生経路へ漏らさず警告ログに落とす。
        /// </summary>
        public event Action<string>? TextMissed;

        /// <param name="tableReference">原文キーを引く String Table Collection 名（または TableReference）</param>
        /// <param name="sourceLocaleCode">
        /// 原文（執筆言語）のロケールコード（例 "ja"）。選択中ロケールがこれと一致する間はテーブルを
        /// 引かず常に原文を返す（原文ロケールのテーブル整備を不要にする・ADR のキー戦略）。
        /// 地域バリアント（"ja" に対する ja-JP 等）も一致扱い。null なら常にテーブルを引く。
        /// </param>
        public LocalizedTableTextResolver(TableReference tableReference, string? sourceLocaleCode = null)
        {
            _tableReference = tableReference;
            _sourceLocaleCode = sourceLocaleCode;
            // ロケール変更の購読は InitializeAsync で行う（ここで静的イベントに触れると LocalizationSettings の
            // 生成 = 設定アセットロードを DI 解決時に強制してしまい、ノベルパートに入らない起動でもコストを払う）
        }

        /// <summary>
        /// Unity Localization の初期化とテーブルの preload。PlayAsync より前に await する契約。
        /// 完了時点で現在ロケールのテーブルがロード済み（またはロード失敗を警告済み）になる。
        /// テーブル不在・ロード失敗は警告ログを残して原文フォールバックに落とす（再生は妨げない）。
        /// </summary>
        public async UniTask InitializeAsync(CancellationToken ct)
        {
            if (_disposed) throw new ObjectDisposedException(nameof(LocalizedTableTextResolver));
            _initializeRequested = true;
            if (!_subscribed)
            {
                // ロケール切替は「次の resolve から反映」。旧ロケールの訳を出し続けないよう即座にテーブルを
                // 捨て、再ロード完了までは原文フォールバックになる
                LocalizationSettings.SelectedLocaleChanged += OnSelectedLocaleChanged;
                _subscribed = true;
            }
            try
            {
                await LocalizationSettings.InitializationOperation.ToUniTask(cancellationToken: ct);
            }
            catch (OperationCanceledException)
            {
                throw;   // キャンセルだけは呼び出し元の関心事
            }
            catch (Exception e) when (e is not OutOfMemoryException)
            {
                // 契約どおりフェイルセーフ: 初期化失敗もテーブル取得失敗と同様に警告 + 原文フォールバック
                Debug.LogWarning($"[Novel] Unity Localization の初期化に失敗: {e.GetType().Name}: {e.Message}。原文のまま表示します。");
                return;
            }
            // ロード中にロケール切替が入ると自分の結果は破棄される（fire-and-forget 側が最新）。その場合も
            // 「InitializeAsync が戻ったらロード済み」の契約を守るため、結果が最新として確定するまでやり直す
            while (!_disposed && !await ReloadTableAsync(ct))
            {
            }
        }

        public string Resolve(string raw)
        {
            if (_disposed) throw new ObjectDisposedException(nameof(LocalizedTableTextResolver));
            if (_isSourceLocale || string.IsNullOrEmpty(raw)) return raw;
            var entries = _entries;
            if (entries == null)
            {
                // 未初期化・切替中・ロード失敗は原文フォールバック。ただし InitializeAsync が一度も
                // 呼ばれていない契約違反だけは黙らない（全行原文のまま無診断で出荷される事故を防ぐ）
                if (!_initializeRequested && !_warnedResolveBeforeInit && Debug.isDebugBuild)
                {
                    _warnedResolveBeforeInit = true;
                    Debug.LogWarning($"[Novel] InitializeAsync が await されないまま resolve されました table='{_tableReference}'。テーブル未ロードのため原文のまま表示します。");
                }
                return raw;
            }

            if (entries.TryGetValue(raw, out var value)) return value;
            NotifyTextMissed(raw);   // 抽出漏れ・訳抜けの dev 収集フック
            return raw;
        }

        private void NotifyTextMissed(string raw)
        {
            var handlers = TextMissed;
            if (handlers == null)
            {
                if (!_warnedTextMissedDefault && Debug.isDebugBuild)
                {
                    _warnedTextMissedDefault = true;
                    Debug.LogWarning($"[Novel] 訳の付かない原文がありました（初回のみ表示）table='{_tableReference}': {raw}");
                }
                return;
            }
            try
            {
                handlers(raw);
            }
            catch (Exception e) when (e is not OutOfMemoryException)
            {
                // resolver は再生経路に居るため例外を漏らさない（漏れると翻訳ミス 1 件で再生全体が Faulted になる）
                Debug.LogWarning($"[Novel] TextMissed 購読者が例外を投げました: {e.GetType().Name}: {e.Message}");
            }
        }

        private void OnSelectedLocaleChanged(Locale _)
        {
            _entries = null;
            ReloadFireAndForget().Forget();
        }

        private async UniTaskVoid ReloadFireAndForget()
        {
            // 失敗の警告は ReloadTableAsync 内で出る。ここは fire-and-forget の未観測例外化を防ぐだけ
            // (ct=None のため OCE はまず来ない)
            try { await ReloadTableAsync(CancellationToken.None); }
            catch (OperationCanceledException) { }
        }

        // 戻り値: この要求の結果が最新として確定したか。後発の切替/破棄に破棄された場合のみ false
        // (ロード失敗は警告 + 原文フォールバックで「確定」なので true)
        private async UniTask<bool> ReloadTableAsync(CancellationToken ct)
        {
            var generation = ++_reloadGeneration;   // この要求を最新とし、先行の in-flight ロード結果を無効化する
            try
            {
                var selected = LocalizationSettings.SelectedLocale;
                _isSourceLocale = IsSourceLocale(selected);
                if (_isSourceLocale)
                {
                    _entries = null;   // 原文ロケールはテーブル不要（キー自体が表示文字列）
                    return true;
                }

                var table = await LocalizationSettings.StringDatabase.GetTableAsync(_tableReference).ToUniTask(cancellationToken: ct);
                if (_disposed || generation != _reloadGeneration) return false;   // 後発の切替/破棄が来ていたら結果を捨てる
                _entries = table != null ? Flatten(table) : null;
                if (table == null)
                    Debug.LogWarning($"[Novel] String Table が見つかりません table='{_tableReference}'。原文のまま表示します。");
                return true;
            }
            catch (OperationCanceledException)
            {
                // キャンセルは世代を消費しない: 自分が最新のままなら世代を返上し、並行中の正当なリロード
                // （ロケール切替の fire-and-forget 等）の完走結果まで道連れに破棄しないようにする
                if (generation == _reloadGeneration) _reloadGeneration--;
                throw;
            }
            catch (Exception e) when (e is not OutOfMemoryException)
            {
                if (_disposed || generation != _reloadGeneration) return false;
                _entries = null;
                Debug.LogWarning($"[Novel] String Table のロードに失敗 table='{_tableReference}': {e.GetType().Name}: {e.Message}。原文のまま表示します。");
                return true;
            }
        }

        // 完全一致に加え、地域バリアントも原文扱いにする（"ja" 指定で ja-JP の Locale アセットを弾かない・逆も同様）
        private bool IsSourceLocale(Locale? selected)
        {
            if (_sourceLocaleCode == null || selected == null) return false;
            string code = selected.Identifier.Code;
            if (string.IsNullOrEmpty(code)) return false;
            if (string.Equals(code, _sourceLocaleCode, StringComparison.OrdinalIgnoreCase)) return true;
            return code.StartsWith(_sourceLocaleCode + "-", StringComparison.OrdinalIgnoreCase) ||
                   _sourceLocaleCode.StartsWith(code + "-", StringComparison.OrdinalIgnoreCase);
        }

        private static Dictionary<string, string> Flatten(StringTable table)
        {
            var entries = new Dictionary<string, string>(table.Count);
            foreach (var entry in table.Values)
            {
                if (entry == null) continue;
                var key = entry.Key;
                var value = entry.Value;
                if (!string.IsNullOrEmpty(key) && !string.IsNullOrEmpty(value)) entries[key] = value;
            }
            return entries;
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;      // Resolve/InitializeAsync/ReloadTableAsync のガードが読む（use-after-dispose を黙って動かさない）
            _entries = null;
            _reloadGeneration++;   // 世代取得済みの in-flight ロード結果を無効化（未取得分は _disposed チェックが止める）
            if (_subscribed)
            {
                LocalizationSettings.SelectedLocaleChanged -= OnSelectedLocaleChanged;
                _subscribed = false;
            }
        }
    }
}
