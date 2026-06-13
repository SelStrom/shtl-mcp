using System;
using System.Linq;

namespace ShtlMcp.Common
{
    public static class ServerName
    {
        public static string Sanitize(string productName)
        {
            var chars = (productName ?? "")
                .Select(c => char.IsLetterOrDigit(c) ? char.ToLowerInvariant(c) : '-');
            string s = new string(chars.ToArray());
            while (s.Contains("--"))
            {
                s = s.Replace("--", "-");
            }
            return s.Trim('-');
        }

        /// livePathForName(name) — путь живого инстанса с таким serverName, или null.
        public static string Resolve(string productName, string projectPath, Func<string, string> livePathForName)
        {
            string baseName = "unity-" + Sanitize(productName);
            string existing = livePathForName(baseName);
            if (existing == null || existing == projectPath)
            {
                return baseName;
            }
            return baseName + "-" + Fnv.Hash4(projectPath);
        }
    }
}
