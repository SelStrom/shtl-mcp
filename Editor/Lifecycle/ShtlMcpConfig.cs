using UnityEditor;

namespace Shtl.Mcp.Lifecycle
{
    /// Конфиг сервера — **machine-local через EditorPrefs** (НЕ per-project committed-asset). Это осознанное
    /// решение (M4/T3): footgun `AllowRunCsharp` не должен «ездить» с проектом через VC (клон чужого проекта не
    /// должен молча включать исполнение произвольного C#); Enabled — выбор разработчика, не команды; port range —
    /// избегание коллизий на машине; heartbeat — перф. AC2.1 допускает EditorPrefs. Footgun по умолчанию выключен
    /// и меняется ТОЛЬКО человеком (не через MCP-тул).
    public static class ShtlMcpConfig
    {
        const string EnabledKey = "Shtl.Mcp.Enabled";
        const string PortStartKey = "Shtl.Mcp.PortRangeStart";
        const string PortCountKey = "Shtl.Mcp.PortRangeCount";
        const string HeartbeatKey = "Shtl.Mcp.HeartbeatSeconds";
        const string RunCsharpKey = "Shtl.Mcp.AllowRunCsharp";
        const string KeepAliveKey = "Shtl.Mcp.IdleKeepAlive";

        /// Мастер-флаг авто-старта: watchdog поднимает сервер только когда true.
        public static bool Enabled
        {
            get => EditorPrefs.GetBool(EnabledKey, true);
            set => EditorPrefs.SetBool(EnabledKey, value);
        }

        public static int PortRangeStart
        {
            get => EditorPrefs.GetInt(PortStartKey, 9700);
            set => EditorPrefs.SetInt(PortStartKey, value);
        }

        public static int PortRangeCount
        {
            get => EditorPrefs.GetInt(PortCountKey, 100);
            set => EditorPrefs.SetInt(PortCountKey, value);
        }

        /// Период тика watchdog/heartbeat (сек). Clamp ≥1.
        public static int HeartbeatSeconds
        {
            get
            {
                int v = EditorPrefs.GetInt(HeartbeatKey, 1);
                return v < 1 ? 1 : v;
            }
            set => EditorPrefs.SetInt(HeartbeatKey, value);
        }

        /// Footgun: разрешает `run_csharp` (компиляция+исполнение произвольного Editor-C#). Default false.
        /// ⚠ ОБЯЗАН оставаться machine-local (EditorPrefs): перенос в committable/project-asset = security-регресс
        /// (клон проекта молча включил бы исполнение кода). Меняется только человеком из дашборда.
        public static bool AllowRunCsharp
        {
            get => EditorPrefs.GetBool(RunCsharpKey, false);
            set => EditorPrefs.SetBool(RunCsharpKey, value);
        }

        /// idle-keepalive (F4/AC4.10): держать редактор в No-Throttling, пока сервер включён, чтобы в фоне
        /// главный поток тикал (MCP + control-flag доступны). Default OFF — компромисс idle-CPU/батарея.
        public static bool IdleKeepAlive
        {
            get => EditorPrefs.GetBool(KeepAliveKey, false);
            set => EditorPrefs.SetBool(KeepAliveKey, value);
        }
    }
}
