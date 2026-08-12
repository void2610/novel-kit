#nullable enable
using System.Collections.Generic;
using Novel.Runtime;
using NUnit.Framework;

namespace Novel.Tests
{
    public sealed class NovelTextVariablesTests
    {
        private static string Expand(string text, Dictionary<string, string>? vars = null, List<string>? missing = null)
            => NovelTextVariables.Expand(text,
                name => vars != null && vars.TryGetValue(name, out var v) ? v : null,
                missing != null ? missing.Add : null);

        [Test]
        public void 変数を値へ展開する()
        {
            var vars = new Dictionary<string, string> { ["gold"] = "500" };
            Assert.AreEqual("所持金は500Gだ", Expand("所持金は%{gold}Gだ", vars));
        }

        [Test]
        public void 複数変数と文中位置の自由な移動に対応する()
        {
            var vars = new Dictionary<string, string> { ["gold"] = "500", ["name"] = "アリス" };
            // 訳文で語順が変わっても展開できる (プレースホルダは訳文中を自由に動かせる)
            Assert.AreEqual("Alice—アリス—has 500G.", Expand("Alice—%{name}—has %{gold}G.", vars));
        }

        [Test]
        public void 未定義変数はプレースホルダのまま残しonMissingへ通知する()
        {
            var missing = new List<string>();
            Assert.AreEqual("未定義は%{unknown}のまま", Expand("未定義は%{unknown}のまま", null, missing));
            CollectionAssert.AreEqual(new[] { "unknown" }, missing);
        }

        [Test]
        public void エスケープと変数でない記号を温存する()
        {
            var vars = new Dictionary<string, string> { ["x"] = "1" };
            Assert.AreEqual("リテラル%{x}と値1", Expand("リテラル%%{x}と値%{x}", vars));   // %%{ → リテラル %{
            Assert.AreEqual("進捗は100%だ", Expand("進捗は100%だ", vars));                // 単独の % は無関係
            Assert.AreEqual("閉じない%{はそのまま", Expand("閉じない%{はそのまま", vars));  // 不成立は温存
            Assert.AreEqual("%{日本語}は変数名でない", Expand("%{日本語}は変数名でない", vars));
        }

        [Test]
        public void 変数を含まないテキストは同一参照で素通しする()
        {
            const string text = "変数の無い普通のセリフ";
            Assert.AreSame(text, Expand(text));   // 無コスト経路 (呼び出し側の再計算スキップが効く)
        }

        [Test]
        public void CreateLookupはprovider優先でstate値へフォールバックする()
        {
            var state = new FakeState();
            state.Set("gold", 500);
            state.Set("both", 1);
            var provider = new FakeProvider { ["name"] = "アリス", ["both"] = "上書き" };
            var lookup = NovelTextVariables.CreateLookup(provider, state);

            Assert.AreEqual("アリス", lookup("name"));    // provider のみ
            Assert.AreEqual("500", lookup("gold"));      // state フォールバック
            Assert.AreEqual("上書き", lookup("both"));    // provider 優先
            Assert.IsNull(lookup("unknown"));            // どちらにも無い → 未定義
        }

        private sealed class FakeProvider : Dictionary<string, string>, ITextVariableProvider
        {
            public bool TryGet(string name, out string value) => TryGetValue(name, out value!);
        }

        private sealed class FakeState : IStateStore
        {
            private readonly Dictionary<string, int> _values = new();
            public int Get(string key) => _values.TryGetValue(key, out var v) ? v : 0;
            public void Set(string key, int value) => _values[key] = value;
            public void Unset(string key) => _values.Remove(key);
            public bool Has(string key) => _values.ContainsKey(key);
            public bool IsRead(string textId) => false;
            public void MarkRead(string textId) { }
        }
    }
}
