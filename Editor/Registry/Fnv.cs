namespace Shtl.Mcp.Common
{
    /// FNV-1a по байтам UTF-16 — детерминирован между процессами (в отличие от string.GetHashCode).
    public static class Fnv
    {
        public static uint Hash32(string s)
        {
            const uint offset = 2166136261u, prime = 16777619u;
            uint h = offset;
            foreach (char c in s)
            {
                h ^= (byte)(c & 0xFF);        h *= prime;
                h ^= (byte)((c >> 8) & 0xFF); h *= prime;
            }
            return h;
        }

        public static string Hash4(string s) => (Hash32(s) & 0xFFFF).ToString("x4");
    }
}
