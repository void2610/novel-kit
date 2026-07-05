#nullable enable
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace Novel.Runtime
{
    // NovelStateSnapshot(フラグ/変数 + 既読)の JSON 直列化をライブラリ側で所有する。
    // color-recollection では ScenarioFlags(Dictionary<string,int>) と read-flags が別々に game 側で
    // シリアライズされていたが、novel-kit は単一 IStateStore に統合済み(state-model ADR)なので、
    // その Capture 結果 = NovelStateSnapshot を 1 つの JSON にまとめて持つ。
    //
    // 形式(安定・決定的。キー/既読 id は序数ソートで並べ、diff/テストを安定させる):
    //   {"version":1,"values":{"coins":30,"met_taylor":1},"read":["a1b2c3d4e5f60718"]}
    //
    // 純 C#(UnityEngine 非依存)。IO は持たず string ⇔ snapshot のみ。永続先(PlayerPrefs/File)は
    // INovelSaveBlobStore に委譲する(JsonSaveStore が両者を束ねる)。
    public static class NovelSaveSerializer
    {
        // 保存形式のバージョン。将来スキーマを変える際の移行フック(現状は読み取るだけ)。
        public const int FormatVersion = 1;

        // 空(セーブ未作成/破損時のフォールバック)の snapshot。
        public static NovelStateSnapshot Empty
            => new(new Dictionary<string, int>(), Array.Empty<string>());

        public static string Serialize(NovelStateSnapshot snapshot)
        {
            var sb = new StringBuilder(256);
            sb.Append("{\"version\":").Append(FormatVersion.ToString(CultureInfo.InvariantCulture));

            sb.Append(",\"values\":{");
            var keys = new List<string>(snapshot.Values.Keys);
            keys.Sort(StringComparer.Ordinal);
            for (var i = 0; i < keys.Count; i++)
            {
                if (i > 0) sb.Append(',');
                WriteString(sb, keys[i]);
                sb.Append(':').Append(snapshot.Values[keys[i]].ToString(CultureInfo.InvariantCulture));
            }

            sb.Append("},\"read\":[");
            var ids = new List<string>(snapshot.ReadTextIds);
            ids.Sort(StringComparer.Ordinal);
            for (var i = 0; i < ids.Count; i++)
            {
                if (i > 0) sb.Append(',');
                WriteString(sb, ids[i]);
            }

            sb.Append("]}");
            return sb.ToString();
        }

        // 破損/未作成に強い読み取り。null/空/不正な JSON は false + Empty を返す(セーブ破損 → 新規開始)。
        public static bool TryDeserialize(string? json, out NovelStateSnapshot snapshot)
        {
            if (string.IsNullOrWhiteSpace(json))
            {
                snapshot = Empty;
                return false;
            }

            try
            {
                snapshot = Deserialize(json!);
                return true;
            }
            catch (NovelSaveFormatException)
            {
                snapshot = Empty;
                return false;
            }
        }

        // 厳密読み取り。不正な JSON / 非オブジェクトルートは NovelSaveFormatException。
        // 未知フィールドは無視、"values"/"read" 欠落は空として許容する(前方/後方互換)。
        public static NovelStateSnapshot Deserialize(string json)
        {
            if (new JsonParser(json).Parse() is not Dictionary<string, object?> root)
                throw new NovelSaveFormatException("root is not a JSON object");

            var values = new Dictionary<string, int>();
            if (root.TryGetValue("values", out var vo) && vo is Dictionary<string, object?> vd)
                foreach (var kv in vd)
                    values[kv.Key] = ToInt(kv.Value);

            var read = new List<string>();
            if (root.TryGetValue("read", out var ro) && ro is List<object?> ra)
                foreach (var e in ra)
                    if (e is string s)
                        read.Add(s);

            return new NovelStateSnapshot(values, read);
        }

        private static int ToInt(object? value)
        {
            switch (value)
            {
                case long l: return unchecked((int)l);
                case double d: return unchecked((int)d);
                case string s when int.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out var si):
                    return si;
                default: return 0;
            }
        }

        private static void WriteString(StringBuilder sb, string s)
        {
            sb.Append('"');
            foreach (var c in s)
            {
                switch (c)
                {
                    case '"': sb.Append("\\\""); break;
                    case '\\': sb.Append("\\\\"); break;
                    case '\b': sb.Append("\\b"); break;
                    case '\f': sb.Append("\\f"); break;
                    case '\n': sb.Append("\\n"); break;
                    case '\r': sb.Append("\\r"); break;
                    case '\t': sb.Append("\\t"); break;
                    default:
                        if (c < 0x20)
                            sb.Append("\\u").Append(((int)c).ToString("x4", CultureInfo.InvariantCulture));
                        else
                            sb.Append(c);
                        break;
                }
            }

            sb.Append('"');
        }

        // このスキーマに必要な範囲を満たす最小の再帰下降 JSON パーサ(object/array/string/number/bool/null)。
        // 生成物を確実に往復できることが目的で、値グラフは string / long / double / bool / null /
        // List<object?> / Dictionary<string,object?> で返す。
        private sealed class JsonParser
        {
            private readonly string _s;
            private int _i;

            public JsonParser(string s) => _s = s;

            public object? Parse()
            {
                SkipWs();
                var v = ParseValue();
                SkipWs();
                if (_i != _s.Length) throw Err("trailing characters");
                return v;
            }

            private object? ParseValue()
            {
                SkipWs();
                if (_i >= _s.Length) throw Err("unexpected end of input");
                var c = _s[_i];
                switch (c)
                {
                    case '{': return ParseObject();
                    case '[': return ParseArray();
                    case '"': return ParseString();
                    case 't': return ParseLiteral("true", true);
                    case 'f': return ParseLiteral("false", false);
                    case 'n': return ParseLiteral("null", null);
                    default: return ParseNumber();
                }
            }

            private Dictionary<string, object?> ParseObject()
            {
                var dict = new Dictionary<string, object?>();
                _i++; // '{'
                SkipWs();
                if (Peek() == '}') { _i++; return dict; }
                while (true)
                {
                    SkipWs();
                    if (Peek() != '"') throw Err("expected object key string");
                    var key = ParseString();
                    SkipWs();
                    if (Peek() != ':') throw Err("expected ':' after object key");
                    _i++;
                    dict[key] = ParseValue();
                    SkipWs();
                    var c = Peek();
                    if (c == ',') { _i++; continue; }
                    if (c == '}') { _i++; break; }
                    throw Err("expected ',' or '}' in object");
                }

                return dict;
            }

            private List<object?> ParseArray()
            {
                var list = new List<object?>();
                _i++; // '['
                SkipWs();
                if (Peek() == ']') { _i++; return list; }
                while (true)
                {
                    list.Add(ParseValue());
                    SkipWs();
                    var c = Peek();
                    if (c == ',') { _i++; continue; }
                    if (c == ']') { _i++; break; }
                    throw Err("expected ',' or ']' in array");
                }

                return list;
            }

            private string ParseString()
            {
                _i++; // opening quote
                var sb = new StringBuilder();
                while (true)
                {
                    if (_i >= _s.Length) throw Err("unterminated string");
                    var c = _s[_i++];
                    if (c == '"') break;
                    if (c != '\\') { sb.Append(c); continue; }

                    if (_i >= _s.Length) throw Err("unterminated escape");
                    var e = _s[_i++];
                    switch (e)
                    {
                        case '"': sb.Append('"'); break;
                        case '\\': sb.Append('\\'); break;
                        case '/': sb.Append('/'); break;
                        case 'b': sb.Append('\b'); break;
                        case 'f': sb.Append('\f'); break;
                        case 'n': sb.Append('\n'); break;
                        case 'r': sb.Append('\r'); break;
                        case 't': sb.Append('\t'); break;
                        case 'u':
                            if (_i + 4 > _s.Length) throw Err("truncated \\u escape");
                            var hex = _s.Substring(_i, 4);
                            if (!ushort.TryParse(hex, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var code))
                                throw Err("invalid \\u escape");
                            sb.Append((char)code);
                            _i += 4;
                            break;
                        default: throw Err($"invalid escape '\\{e}'");
                    }
                }

                return sb.ToString();
            }

            private object ParseNumber()
            {
                var start = _i;
                while (_i < _s.Length && IsNumberChar(_s[_i])) _i++;
                if (_i == start) throw Err($"unexpected character '{_s[start]}'");
                var token = _s.Substring(start, _i - start);
                if (long.TryParse(token, NumberStyles.Integer, CultureInfo.InvariantCulture, out var l))
                    return l;
                if (double.TryParse(token, NumberStyles.Float, CultureInfo.InvariantCulture, out var d))
                    return d;
                throw Err($"invalid number '{token}'");
            }

            private object? ParseLiteral(string literal, object? value)
            {
                if (_i + literal.Length > _s.Length || _s.Substring(_i, literal.Length) != literal)
                    throw Err($"expected '{literal}'");
                _i += literal.Length;
                return value;
            }

            private static bool IsNumberChar(char c)
                => c is (>= '0' and <= '9') or '-' or '+' or '.' or 'e' or 'E';

            private char Peek() => _i < _s.Length ? _s[_i] : '\0';

            private void SkipWs()
            {
                while (_i < _s.Length)
                {
                    var c = _s[_i];
                    if (c is ' ' or '\t' or '\n' or '\r') _i++;
                    else break;
                }
            }

            private NovelSaveFormatException Err(string message)
                => new($"{message} (at index {_i})");
        }
    }

    // セーブ JSON が壊れている/形式不正のときに投げる。TryDeserialize はこれを握って Empty を返す。
    public sealed class NovelSaveFormatException : Exception
    {
        public NovelSaveFormatException(string message) : base(message) { }
    }
}
