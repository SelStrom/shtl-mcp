using UnityEditor;

namespace Shtl.Mcp.Lifecycle
{
    public static class ShtlMcpConfig
    {
        const string EnabledKey = "Shtl.Mcp.Enabled";
        public static bool Enabled
        {
            get => EditorPrefs.GetBool(EnabledKey, true);
            set => EditorPrefs.SetBool(EnabledKey, value);
        }
    }
}
