#nullable enable
using System.Collections.Generic;
using Novel.Runtime;

namespace Novel.Editor
{
    /// <summary>
    /// Validate Scenarios に opt-in アセンブリの語彙 (cinematic 等) のキー検証を足すための口。
    /// 検証はスタブ実行で行うため、拡張側は「語彙を登録してキーを記録するモジュール」と「正解集合」を提供する。
    /// </summary>
    public interface IScenarioKeyExtension
    {
        /// <summary>警告文の「未定義の{Label}」に入る種別名。</summary>
        string Label { get; }

        /// <summary>語彙を登録し、実行中に届いたキーを <paramref name="keys"/> へ積むモジュールを作る (実行ごとに新規)。</summary>
        INovelCommandModule CreateRecorder(ISet<string> keys);

        /// <summary>
        /// この語彙の糖衣を定義する preamble。無ければ空。
        /// スタブ実行は core の preamble しか読まないため、これを渡さないと糖衣が未定義 → no-op stub 化されてキーが記録されない。
        /// </summary>
        IEnumerable<IPreambleSource> PreambleSources();

        /// <summary>存在するキーの集合。情報源が無ければ null (= 検証をスキップ。誤警告より見逃しに倒す)。</summary>
        HashSet<string>? ScanKnownKeys();
    }

    public static class ScenarioKeyExtensions
    {
        private static readonly List<IScenarioKeyExtension> Registered = new();

        public static IReadOnlyList<IScenarioKeyExtension> All => Registered;

        public static void Register(IScenarioKeyExtension extension)
        {
            if (!Registered.Contains(extension)) Registered.Add(extension);
        }
    }
}
