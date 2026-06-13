using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using Shtl.Mcp.Lifecycle;

namespace Shtl.Mcp.UI
{
    public sealed class DashboardWindow : EditorWindow
    {
        [MenuItem("Window/Shtl MCP")]
        public static void Open() => GetWindow<DashboardWindow>("Shtl MCP");

        Label _status, _identity, _mode;
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

            Refresh();
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
        }
    }
}
