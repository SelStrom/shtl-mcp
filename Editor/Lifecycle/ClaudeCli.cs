using System.Diagnostics;

namespace Shtl.Mcp.Server
{
    /// Запуск `claude` CLI вне Dashboard (переиспользуется lifecycle для ре-регистрации порта под --scope user).
    public static class ClaudeCli
    {
        public static string AddUserScopeArgs(string serverName, int port)
            => $"mcp add --transport http --scope user {serverName} http://127.0.0.1:{port}/mcp";

        // PATH GUI-приложения (Unity) минимален и login-shell его не чинит (claude в ~/.local/bin не виден).
        // Поэтому резолвим бинарь напрямую по типичным путям.
        static string ClaudeBin()
        {
            var home = System.Environment.GetFolderPath(System.Environment.SpecialFolder.UserProfile);
            string[] cand =
            {
                home + "/.local/bin/claude",
                "/opt/homebrew/bin/claude",
                "/usr/local/bin/claude",
                home + "/.npm-global/bin/claude",
                home + "/bin/claude",
                home + "/.bun/bin/claude"
            };
            foreach (var c in cand)
            {
                if (System.IO.File.Exists(c))
                {
                    return c;
                }
            }
            return "claude"; // последняя надежда — вдруг в PATH процесса
        }

        // Вызывать с ФОНОВОГО потока (Process.WaitForExit блокирует). Возвращает (exit, output).
        public static (int exit, string output) Run(string args, int timeoutMs)
        {
            try
            {
                var psi = new ProcessStartInfo(ClaudeBin(), args)
                {
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };
                var home = System.Environment.GetFolderPath(System.Environment.SpecialFolder.UserProfile);
                var cur = psi.EnvironmentVariables.ContainsKey("PATH") ? psi.EnvironmentVariables["PATH"] : "";
                psi.EnvironmentVariables["PATH"] = home + "/.local/bin:/opt/homebrew/bin:/usr/local/bin:/usr/bin:/bin:" + cur;

                using (var p = Process.Start(psi))
                {
                    // Читать пайпы ДО ожидания выхода: дочерний процесс с выводом больше буфера пайпа
                    // блокируется на write и никогда не выйдет (взаимная блокировка с WaitForExit).
                    var outTask = p.StandardOutput.ReadToEndAsync();
                    var errTask = p.StandardError.ReadToEndAsync();
                    if (!p.WaitForExit(timeoutMs))
                    {
                        try { p.Kill(); } catch { }
                        return (-1, "timeout");
                    }
                    return (p.ExitCode, (outTask.Result + errTask.Result).Trim());
                }
            }
            catch (System.Exception e)
            {
                return (-1, e.Message);
            }
        }
    }
}
