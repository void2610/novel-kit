#nullable enable
using System;
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
    /// 未初期化中の resolve は原文フォールバック。
    ///
    /// ロケール切替（SelectedLocale 変更）は自動で追従するが「次の resolve から反映」であり、
    /// 表示中の行・バックログの遡及再描画はしない（ADR）。切替直後〜再ロード完了までは原文フォールバック。
    /// 確実に切り替えてからノベルを再開したい game は、切替後にもう一度 InitializeAsync を await する。
    ///
    /// DI: RegisterNovelKitCore() の既定 IdentityTextResolver を後勝ち登録で上書きする。
    /// <code>builder.Register&lt;ITextResolver&gt;(_ =&gt; new LocalizedTableTextResolver("NovelText", "ja"), Lifetime.Singleton);</code>
    /// </summary>
    public sealed class LocalizedTableTextResolver : ITextResolver, IDisposable
    {
        private readonly TableReference _tableReference;
        private readonly string? _sourceLocaleCode;
        private StringTable? _table;
        private bool _isSourceLocale;
        private bool _disposed;
        // 再ロードの世代番号。ロケール連打 (en→fr) でロード完了が逆順に届いても、最新要求以外の結果を
        // 捨てて古いロケールのテーブルが居座らないようにする (Unity メインスレッド前提の単純カウンタ)
        private int _reloadGeneration;

        /// <summary>
        /// 訳が付かなかった原文の通知（dev 抽出漏れ収集用）。テーブルロード済み・非原文ロケールでの
        /// ミスのみ発火する（未初期化中や原文ロケールでは発火しない）。購読は任意。
        /// </summary>
        public event Action<string>? TextMissed;

        /// <param name="tableReference">原文キーを引く String Table Collection 名（または TableReference）</param>
        /// <param name="sourceLocaleCode">
        /// 原文（執筆言語）のロケールコード（例 "ja"）。選択中ロケールがこれと一致する間はテーブルを
        /// 引かず常に原文を返す（原文ロケールのテーブル整備を不要にする・ADR のキー戦略）。
        /// null なら常にテーブルを引く。
        /// </param>
        public LocalizedTableTextResolver(TableReference tableReference, string? sourceLocaleCode = null)
        {
            _tableReference = tableReference;
            _sourceLocaleCode = sourceLocaleCode;
            // ロケール切替は「次の resolve から反映」。旧ロケールの訳を出し続けないよう即座にテーブルを
            // 捨て、再ロード完了までは原文フォールバックになる
            LocalizationSettings.SelectedLocaleChanged += OnSelectedLocaleChanged;
        }

        /// <summary>
        /// Unity Localization の初期化とテーブルの preload。PlayAsync より前に await する契約。
        /// テーブル不在・ロード失敗は警告ログを残して原文フォールバックに落とす（再生は妨げない）。
        /// </summary>
        public async UniTask InitializeAsync(CancellationToken ct)
        {
            try
            {
                await LocalizationSettings.InitializationOperation.ToUniTask(cancellationToken: ct);
            }
            catch (OperationCanceledException)
            {
                throw;   // キャンセルだけは呼び出し元の関心事
            }
            catch (Exception e)
            {
                // 契約どおりフェイルセーフ: 初期化失敗もテーブル取得失敗と同様に警告 + 原文フォールバック
                Debug.LogWarning($"[Novel] Unity Localization の初期化に失敗: {e.GetType().Name}: {e.Message}。原文のまま表示します。");
                return;
            }
            await ReloadTableAsync(ct);
        }

        public string Resolve(string raw)
        {
            if (_isSourceLocale || string.IsNullOrEmpty(raw)) return raw;
            var table = _table;
            if (table == null) return raw;   // 未初期化・切替中・ロード失敗は原文フォールバック

            var value = table.GetEntry(raw)?.Value;
            if (string.IsNullOrEmpty(value))
            {
                TextMissed?.Invoke(raw);     // 抽出漏れ・訳抜けの dev 収集フック
                return raw;
            }
            return value!;
        }

        private void OnSelectedLocaleChanged(Locale _)
        {
            _table = null;
            ReloadFireAndForget().Forget();
        }

        private async UniTaskVoid ReloadFireAndForget()
        {
            try
            {
                await ReloadTableAsync(CancellationToken.None);
            }
            catch (Exception e)
            {
                // resolver は再生経路に居るため例外を漏らさない。失敗時は原文フォールバックのまま
                Debug.LogWarning($"[Novel] ロケール切替後のテーブル再ロードに失敗 table='{_tableReference}': {e.GetType().Name}: {e.Message}");
            }
        }

        private async UniTask ReloadTableAsync(CancellationToken ct)
        {
            var generation = ++_reloadGeneration;   // この要求を最新とし、先行の in-flight ロード結果を無効化する
            var selected = LocalizationSettings.SelectedLocale;
            _isSourceLocale = _sourceLocaleCode != null && selected != null &&
                              selected.Identifier.Code == _sourceLocaleCode;
            if (_isSourceLocale)
            {
                _table = null;   // 原文ロケールはテーブル不要（キー自体が表示文字列）
                return;
            }

            try
            {
                var table = await LocalizationSettings.StringDatabase.GetTableAsync(_tableReference).ToUniTask(cancellationToken: ct);
                if (generation != _reloadGeneration) return;   // 後発の切替/破棄が来ていたら結果を捨てる
                _table = table;
                if (table == null)
                    Debug.LogWarning($"[Novel] String Table が見つかりません table='{_tableReference}'。原文のまま表示します。");
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception e)
            {
                if (generation != _reloadGeneration) return;
                _table = null;
                Debug.LogWarning($"[Novel] String Table のロードに失敗 table='{_tableReference}': {e.GetType().Name}: {e.Message}。原文のまま表示します。");
            }
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            _reloadGeneration++;   // in-flight ロードの結果を無効化 (破棄後の _table 書き込みを防ぐ)
            LocalizationSettings.SelectedLocaleChanged -= OnSelectedLocaleChanged;
        }
    }
}
