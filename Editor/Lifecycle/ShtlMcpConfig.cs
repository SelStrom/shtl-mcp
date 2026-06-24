using UnityEditor;

namespace Shtl.Mcp.Lifecycle
{
    /// Конфиг сервера (бэкенд, per-machine через EditorPrefs; UI-доводка → M3). Footgun-флаг
    /// `AllowRunCsharp` по умолчанию выключен и меняется ТОЛЬКО человеком (не через MCP-тул).
    public static class ShtlMcpConfig
    {
        const string EnabledKey = "Shtl.Mcp.Enabled";
        const string PortStartKey = "Shtl.Mcp.PortRangeStart";
        const string PortCountKey = "Shtl.Mcp.PortRangeCount";
        const string HeartbeatKey = "Shtl.Mcp.HeartbeatSeconds";
        const string RunCsharpKey = "Shtl.Mcp.AllowRunCsharp";

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
        public static bool AllowRunCsharp
        {
            get => EditorPrefs.GetBool(RunCsharpKey, false);
            set => EditorPrefs.SetBool(RunCsharpKey, value);
        }
    }
}
