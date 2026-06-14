#nullable enable
namespace Novel.Runtime
{
    // 既読判定用の決定的な安定ハッシュ（FNV-1a 64bit）。話者 + 本文から算出。
    // 64bit にして衝突確率を下げる（全作品の総ユニーク台詞数が増えても誤既読が起きにくい）。
    internal static class StableId
    {
        private const ulong FnvOffset = 14695981039346656037UL;
        private const ulong FnvPrime = 1099511628211UL;

        public static string Of(string? speakerId, string text)
        {
            ulong hash = FnvOffset;
            hash = Fnv(hash, speakerId ?? "");
            hash ^= 0x1F;           // 話者と本文の区切り
            hash *= FnvPrime;
            hash = Fnv(hash, text);
            return hash.ToString("x16");
        }

        private static ulong Fnv(ulong hash, string s)
        {
            foreach (char c in s)
            {
                hash ^= c;
                hash *= FnvPrime;
            }
            return hash;
        }
    }
}
