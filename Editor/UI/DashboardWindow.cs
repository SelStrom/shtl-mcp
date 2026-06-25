using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;
using Shtl.Mcp.Lifecycle;

namespace Shtl.Mcp.UI
{
    public sealed class DashboardWindow : EditorWindow
    {
        [MenuItem("Window/Shtl MCP")]
        public static void Open() => GetWindow<DashboardWindow>("Shtl MCP");

        Label _status, _identity, _mode, _reloadDomain;
        TextField _cmd;
        double _next;

        public void CreateGUI()
        {
            var root = rootVisualElement;
            root.style.paddingLeft = root.style.paddingTop = 8;
            _status = new Label();
            _identity = new Label();
            _mode = new Label();
            root.Add(_status);
            root.Add(_identity);
            root.Add(_mode);

            root.Add(new Label("claude mcp add command:") { style = { marginTop = 8 } });
            _cmd = new TextField { isReadOnly = true, multiline = true };
            root.Add(_cmd);

            var copy = new Button(() => EditorGUIUtility.systemCopyBuffer = _cmd.value) { text = "Copy" };
            var restart = new Button(() => ShtlMcpServer.Instance.RestartNow()) { text = "Restart server" };
            var row = new VisualElement { style = { flexDirection = FlexDirection.Row, marginTop = 8 } };
            row.Add(copy);
            row.Add(restart);
            root.Add(row);

            // Enter Play Mode → Reload Domain (AC4.4): статус + кнопка применить рекомендуемое (OFF).
            root.Add(Header("Play mode"));
            _reloadDomain = new Label();
            root.Add(_reloadDomain);
            root.Add(new Button(ApplyReloadDomainOff)
            {
                text = "Apply recommended (Reload Domain OFF)",
                tooltip = "Включает Enter Play Mode Options + DisableDomainReload — listener переживает вход в Play."
            });

            // Config (per-machine, EditorPrefs). Footgun-тогл здесь = human-only гейт run_csharp.
            root.Add(Header("Config"));
            BindToggle(root, "Server enabled (auto-start)", ShtlMcpConfig.Enabled, v => ShtlMcpConfig.Enabled = v);
            BindToggle(root, "Allow run_csharp  ⚠ FOOTGUN", ShtlMcpConfig.AllowRunCsharp, v => ShtlMcpConfig.AllowRunCsharp = v);
            BindInt(root, "Port range start", ShtlMcpConfig.PortRangeStart, v => ShtlMcpConfig.PortRangeStart = v);
            BindInt(root, "Port range count", ShtlMcpConfig.PortRangeCount, v => ShtlMcpConfig.PortRangeCount = v);
            BindInt(root, "Heartbeat seconds", ShtlMcpConfig.HeartbeatSeconds, v => ShtlMcpConfig.HeartbeatSeconds = v);

            Refresh();
        }

        static Label Header(string text) => new Label(text)
        {
            style = { marginTop = 10, unityFontStyleAndWeight = FontStyle.Bold }
        };

        static void BindToggle(VisualElement root, string label, bool initial, System.Action<bool> set)
        {
            var t = new Toggle(label) { value = initial };
            t.RegisterValueChangedCallback(e => set(e.newValue));
            root.Add(t);
        }

        static void BindInt(VisualElement root, string label, int initial, System.Action<int> set)
        {
            var f = new IntegerField(label) { value = initial };
            f.RegisterValueChangedCallback(e => set(e.newValue));
            root.Add(f);
        }

        static bool DomainReloadOff()
        {
            return EditorSettings.enterPlayModeOptionsEnabled
                && (EditorSettings.enterPlayModeOptions & EnterPlayModeOptions.DisableDomainReload) != 0;
        }

        static void ApplyReloadDomainOff()
        {
            EditorSettings.enterPlayModeOptionsEnabled = true;
            EditorSettings.enterPlayModeOptions |= EnterPlayModeOptions.DisableDomainReload;
        }

        void Update()
        {
            if (EditorApplication.timeSinceStartup < _next)
            {
                return;
            }
            _next = EditorApplication.timeSinceStartup + 1.0;
            Refresh();
        }

        void Refresh()
        {
            if (_status == null)
            {
                return;
            }
            var s = ShtlMcpServer.Instance;
            bool up = s.IsListening;
            _status.text = (up ? "● running" : "○ stopped") + "   " + s.ServerName + "   :" + s.Port;
            _identity.text = "project: " + Application.productName;
            _mode.text = "mode: " + (EditorApplication.isPlaying ? "PLAY" : "EDIT");
            _cmd.value = $"claude mcp add --transport http {s.ServerName} http://127.0.0.1:{s.Port}/mcp";
            _reloadDomain.text = DomainReloadOff()
                ? "Reload Domain: OFF  ✓ (recommended — listener переживает вход в Play без reload)"
                : "Reload Domain: ON  (вход в Play триггерит domain reload; сервер переживает, но ~сек недоступности)";
        }
    }
}
