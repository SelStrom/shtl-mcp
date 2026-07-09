using System.Collections.Generic;
using System.IO;

namespace Shtl.Mcp.Lifecycle
{
    /// F7/AC7.4: opt-in host-крошка. Дашборд ПРЕДЛАГАЕТ дописать в host-`CLAUDE.md` однострочный
    /// recovery-указатель, чтобы свежая LLM-сессия была праймлена даже если сервер уже мёртв (cold-start).
    /// По умолчанию — НИЧЕГО (INV-2): запись только по явному согласию человека из дашборда.
    public static class HostBreadcrumb
    {
        /// Маркер идемпотентности (и якорь для детекта «уже добавлено»).
        public const string Marker = "<!-- shtl-mcp-recovery -->";

        /// Точный markdown-блок, который дописывается (превью в дашборде = ровно это).
        public static string Text()
        {
            return Marker + "\n" +
                "> **Unity MCP recovery (shtl-mcp).** If a `unity-*` MCP tool is unreachable " +
                "(connection refused / timeout), read `~/.unity-mcp/registry.json`: each instance entry has a " +
                "`recovery` block with diagnostic steps and a restart command. The `ping` tool answers even when " +
                "Unity's main thread is blocked (modal dialog / compiling), distinguishing a wedged main thread " +
                "from a dead server.";
        }

        public static bool IsPresent(string content)
        {
            return !string.IsNullOrEmpty(content) && content.Contains(Marker);
        }

        /// Unity-проект часто вложен в репозиторий (`repo/Client/<Project>/`), а Claude Code при
        /// старте грузит CLAUDE.md от корня репозитория — крошка в корне Unity-проекта там не видна.
        /// Кандидаты — каждый уровень от git-корня (директория с `.git`) вниз до корня Unity-проекта;
        /// приоритет: файл с уже добавленным маркером → существующий CLAUDE.md, ближайший к git-корню
        /// → git-корень (файл будет создан). Unity-проект вне git — корень Unity-проекта.
        public static string ResolveTargetPath(string unityProjectRoot)
        {
            var candidates = new List<string>();
            foreach (string dir in CandidateDirs(unityProjectRoot))
            {
                candidates.Add(Path.Combine(dir, "CLAUDE.md"));
            }
            foreach (string path in candidates)
            {
                if (File.Exists(path) && IsPresent(File.ReadAllText(path)))
                {
                    return path;
                }
            }
            foreach (string path in candidates)
            {
                if (File.Exists(path))
                {
                    return path;
                }
            }
            return candidates[0];
        }

        static List<string> CandidateDirs(string unityProjectRoot)
        {
            var chain = new List<string>();
            var dir = new DirectoryInfo(unityProjectRoot);
            while (dir != null)
            {
                chain.Add(dir.FullName);
                string git = Path.Combine(dir.FullName, ".git");
                if (Directory.Exists(git) || File.Exists(git)) // .git-файл: worktree/submodule
                {
                    chain.Reverse(); // git-корень первым — у него приоритет
                    return chain;
                }
                dir = dir.Parent;
            }
            return new List<string> { new DirectoryInfo(unityProjectRoot).FullName };
        }

        /// Идемпотентно дописать крошку. true = записали; false = маркер уже был (no-op). Создаёт файл, если нет.
        public static bool AddTo(string targetPath)
        {
            string existing = File.Exists(targetPath) ? File.ReadAllText(targetPath) : "";
            if (IsPresent(existing))
            {
                return false;
            }
            string sep = existing.Length == 0 ? "" : (existing.EndsWith("\n") ? "\n" : "\n\n");
            File.AppendAllText(targetPath, sep + Text() + "\n");
            return true;
        }
    }
}
