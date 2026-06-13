#nullable enable
namespace Novel.Runtime
{
    // 既読判定用の決定的な安定ハッシュ（FNV-1a 32bit）。話者 + 本文から算出
    internal static class StableId
    {
        public static string Of(string? speakerId, string text)
        {
            uint hash = 2166136261;
            hash = Fnv(hash, speakerId ?? "");
            hash ^= 0x1F;           // 話者と本文の区切り
            hash *= 16777619;
            hash = Fnv(hash, text);
            return hash.ToString("x8");
        }

        private static uint Fnv(uint hash, string s)
        {
            foreach (char c in s)
            {
                hash ^= c;
                hash *= 16777619;
            }
            return hash;
        }
    }
}
