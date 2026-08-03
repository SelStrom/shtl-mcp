using System;
using System.IO;
using System.Linq;

namespace Shtl.Mcp.Common
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
        /// assignedName — имя, уже закреплённое за этим путём (из реестра), или null для нового пути.
        public static string Resolve(string productName, string projectPath, Func<string, string> livePathForName,
            string assignedName = null)
        {
            // Имя закрепляется за путём один раз: клиентская запись `claude mcp add` ключуется именем,
            // поэтому переезд имени на другой инстанс разошёлся бы с уже зарегистрированным адресом.
            if (string.IsNullOrEmpty(assignedName) == false)
            {
                return assignedName;
            }

            return Mint(productName, projectPath, livePathForName);
        }

        static string Mint(string productName, string projectPath, Func<string, string> livePathForName)
        {
            bool IsFree(string name)
            {
                string owner = livePathForName(name);
                return owner == null || owner == projectPath;
            }

            string baseName = "unity-" + Sanitize(productName);
            if (IsFree(baseName))
            {
                return baseName;
            }

            // Имя папки различает клоны и worktrees читаемо, в отличие от хеша пути.
            string folderName = "unity-" + Sanitize(FolderName(projectPath));
            if (folderName != baseName && IsFree(folderName))
            {
                return folderName;
            }

            return baseName + "-" + Fnv.Hash4(projectPath);
        }

        static string FolderName(string projectPath)
        {
            return Path.GetFileName((projectPath ?? "").TrimEnd('/', '\\'));
        }
    }
}
