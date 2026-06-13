using UnityEditor;

namespace ShtlMcp.Lifecycle
{
    public static class ShtlMcpConfig
    {
        const string EnabledKey = "ShtlMcp.Enabled";
        public static bool Enabled
        {
            get => EditorPrefs.GetBool(EnabledKey, true);
            set => EditorPrefs.SetBool(EnabledKey, value);
        }
    }
}
