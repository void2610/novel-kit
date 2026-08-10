#nullable enable
using System;
using System.Collections.Generic;
using System.Text;

namespace Novel.Runtime
{
    // テキスト変数 %{key} のゲーム固有値の供給口 (主人公名など IStateStore の int に載らない値)。
    // 未解決 (false) なら IStateStore のフラグ/変数値へフォールバックする
    public interface ITextVariableProvider
    {
        bool TryGet(string name, out string value);
    }

    // 既定の no-op (常に IStateStore フォールバックへ)。game は ITextVariableProvider の後勝ち登録で拡張する
    public sealed class NullTextVariableProvider : ITextVariableProvider
    {
        public bool TryGet(string name, out string value)
        {
            value = "";
            return false;
        }
    }

    /// <summary>
    /// テキスト変数 <c>%{key}</c> の遅延展開（localization-unity-package ADR）。
    /// Ruby の <c>#{}</c> は発行前に Ruby が即時評価するためテンプレートが C# へ届かず、
    /// 多言語化（キー照合）と既読 ID（値が変わるたび別 ID になる）の両方を壊す。
    /// <c>%{key}</c> は Ruby では不活性の素の文字列なのでテンプレートのまま届き、
    /// **訳の取得後**にここで値を差し込む（翻訳者は訳文中でプレースホルダを自由に動かせる）。
    ///
    /// - 変数名は英数字とアンダースコア（`%{gold}` / `%{player_name}`）
    /// - 未定義の変数はプレースホルダをそのまま残す（黙って消さない。onMissing で警告）
    /// - リテラルに `%{` を書きたい場合は `%%{` とエスケープする（単独の `%` は無関係）
    /// </summary>
    public static class NovelTextVariables
    {
        /// <param name="lookup">変数名 → 表示文字列。null = 未定義（プレースホルダ温存）</param>
        /// <param name="onMissing">未定義変数の通知（変数名）。dev の書き間違い検出用</param>
        public static string Expand(string text, Func<string, string?> lookup, Action<string>? onMissing = null)
        {
            // 変数を含まない行を無コストで素通しする（同一参照を返し、呼び出し側の再計算スキップも効かせる）
            if (text.IndexOf("%{", StringComparison.Ordinal) < 0) return text;

            var sb = new StringBuilder(text.Length + 16);
            for (var i = 0; i < text.Length; i++)
            {
                var c = text[i];
                if (c == '%' && i + 2 < text.Length && text[i + 1] == '%' && text[i + 2] == '{')
                {
                    sb.Append("%{");   // エスケープ %%{ → リテラル %{
                    i += 2;
                    continue;
                }
                if (c == '%' && i + 1 < text.Length && text[i + 1] == '{')
                {
                    var close = FindName(text, i + 2, out var name);
                    if (close >= 0)
                    {
                        var value = lookup(name);
                        if (value != null)
                        {
                            sb.Append(value);
                        }
                        else
                        {
                            sb.Append(text, i, close - i + 1);   // 未定義はそのまま残す (可視 = 気付ける)
                            onMissing?.Invoke(name);
                        }
                        i = close;
                        continue;
                    }
                    // %{ の後が変数名の形をしていない / 閉じていない → 通常文字として温存
                }
                sb.Append(c);
            }
            return sb.ToString();
        }

        // start から [A-Za-z0-9_]+ を読み '}' で閉じていれば '}' の位置を返す。それ以外は -1
        private static int FindName(string text, int start, out string name)
        {
            var i = start;
            while (i < text.Length && (char.IsLetterOrDigit(text[i]) || text[i] == '_')) i++;
            if (i > start && i < text.Length && text[i] == '}')
            {
                name = text.Substring(start, i - start);
                return i;
            }
            name = "";
            return -1;
        }

        // ハンドラ用の標準 lookup 合成: game 供給の provider を優先し、無ければ IStateStore の変数値
        public static Func<string, string?> CreateLookup(ITextVariableProvider? provider, IStateStore state)
            => name =>
            {
                if (provider != null && provider.TryGet(name, out var value)) return value;
                return state.Has(name) ? state.Get(name).ToString() : null;
            };
    }
}
