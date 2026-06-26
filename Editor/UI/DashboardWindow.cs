using System.Diagnostics;
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

        Label _status, _identity, _mode, _client;
        VisualElement _mcpArea;
        Foldout _manual;
        Label _reloadDomainStatus;
        double _next;

        // Фоновый поток (claude) кладёт сюда коллбэк, главный поток (Update) исполняет. EditorApplication.*
        // нельзя трогать с фонового потока — это и была ошибка в консоли.
        readonly System.Collections.Concurrent.ConcurrentQueue<System.Action> _mainQueue
            = new System.Collections.Concurrent.ConcurrentQueue<System.Action>();

        public void CreateGUI()
        {
            var root = rootVisualElement;
            root.style.paddingLeft = root.style.paddingTop = 8;
            _status = new Label();   // своё состояние сервера
            _client = new Label();   // живой коннект LLM-клиента
            _identity = new Label();
            _mode = new Label();
            root.Add(_status);
            root.Add(_client);
            root.Add(_identity);
            root.Add(_mode);

            // Подключение к Claude Code: одна кнопка; если инстанс уже добавлен — прячем кнопку И manual.
            _mcpArea = new VisualElement { style = { marginTop = 8 } };
            root.Add(_mcpArea);

            // Manual fallback (свёрнут): готовая команда. Скрывается вместе с кнопкой Add, когда настроено.
            _manual = new Foldout { text = "Manual add command", value = false };
            Indent(_manual);
            var cmd = new TextField { isReadOnly = true, multiline = true, value = AddCommand() };
            _manual.Add(cmd);
            _manual.Add(new Button(() => EditorGUIUtility.systemCopyBuffer = cmd.value) { text = "Copy" });
            root.Add(_manual);

            RenderMcpArea(null); // null = «проверяем…» (после создания _manual)
            CheckConfiguredAsync();

            // Настройки — в foldout, свёрнуты (дашборд компактный).
            var settings = new Foldout { text = "Settings", value = false };
            Indent(settings);
            root.Add(settings);

            _reloadDomainStatus = new Label();
            settings.Add(_reloadDomainStatus);
            var noReload = new Toggle("Disable domain reload on Enter Play") { value = DomainReloadOff() };
            noReload.tooltip =
                "Зачем: вход в Play по умолчанию выгружает C#-домен (Reload Domain ON) → in-Unity MCP-листенер " +
                "гибнет на ~секунды (re-spawn), и Play стартует медленнее. Этот тогл ON держит домен → MCP " +
                "доступен непрерывно через play/edit, Play быстрее.\n\nКомпромисс: статические поля НЕ " +
                "сбрасываются при входе в Play — код, полагающийся на reload для сброса состояния, может " +
                "потребовать [InitializeOnEnterPlayMode]. Не «всегда правильно» — удобно для MCP-воркфлоу, " +
                "решай по проекту. Unity-дефолт: reload ON (этот тогл OFF).";
            noReload.RegisterValueChangedCallback(e => SetDomainReloadOff(e.newValue));
            settings.Add(noReload);

            BindToggle(settings, "Server enabled (auto-start)", ShtlMcpConfig.Enabled, v => ShtlMcpConfig.Enabled = v);
            BindToggle(settings, "Allow run_csharp  ⚠ FOOTGUN", ShtlMcpConfig.AllowRunCsharp, v => ShtlMcpConfig.AllowRunCsharp = v,
                "Разрешает компиляцию+исполнение произвольного Editor-C# через run_csharp. Опасно: полный " +
                "доступ к проекту/ФС. По умолчанию выкл; включай только осознанно и выключай после.");
            BindInt(settings, "Port range start", ShtlMcpConfig.PortRangeStart, v => ShtlMcpConfig.PortRangeStart = v);
            BindInt(settings, "Port range count", ShtlMcpConfig.PortRangeCount, v => ShtlMcpConfig.PortRangeCount = v);
            BindInt(settings, "Heartbeat seconds", ShtlMcpConfig.HeartbeatSeconds, v => ShtlMcpConfig.HeartbeatSeconds = v);
            settings.Add(new Button(() => ShtlMcpServer.Instance.RestartNow()) { text = "Restart server", style = { marginTop = 6 } });

            Refresh();
        }

        // ── MCP add ──────────────────────────────────────────────────────────

        static string AddCommand()
        {
            var s = ShtlMcpServer.Instance;
            return $"claude mcp add --transport http --scope user {s.ServerName} http://127.0.0.1:{s.Port}/mcp";
        }

        // configured: true = уже добавлен, false = нет, null = проверяем / не удалось узнать.
        void RenderMcpArea(bool? configured)
        {
            if (_mcpArea == null)
            {
                return;
            }
            _mcpArea.Clear();
            if (configured == true)
            {
                _mcpArea.Add(new Label("✓ added to Claude Code") { style = { color = new Color(0.4f, 0.8f, 0.4f) } });
                ShowManual(false); // настроено → manual-команда не нужна
                return;
            }
            var add = new Button(OnAddClicked) { text = "Add to Claude Code" };
            _mcpArea.Add(add);
            if (configured == null)
            {
                _mcpArea.Add(new Label("checking…") { style = { opacity = 0.6f } });
            }
            ShowManual(true); // не настроено/неизвестно → manual доступен как fallback
        }

        void ShowManual(bool show)
        {
            if (_manual != null)
            {
                _manual.style.display = show ? DisplayStyle.Flex : DisplayStyle.None;
            }
        }

        // Уменьшить большой дефолтный отступ содержимого foldout.
        static void Indent(Foldout f) => f.contentContainer.style.marginLeft = 2;

        static string McpAddArgs()
        {
            var s = ShtlMcpServer.Instance;
            return $"mcp add --transport http --scope user {s.ServerName} http://127.0.0.1:{s.Port}/mcp";
        }

        void OnAddClicked()
        {
            RenderMcpArea(null); // «checking…» пока выполняется
            RunClaudeAsync(McpAddArgs(), 8000, (exit, output) =>
            {
                if (exit == 0)
                {
                    RenderMcpArea(true);
                }
                else
                {
                    RenderMcpArea(false);
                    UnityEngine.Debug.LogWarning("[Shtl MCP] `claude mcp add` failed (exit " + exit + "): " + output +
                        "\nИспользуй ‘Manual add command’ или проверь, что CLI `claude` установлен.");
                }
            });
        }

        void CheckConfiguredAsync()
        {
            var name = ShtlMcpServer.Instance.ServerName;
            RunClaudeAsync("mcp list", 8000, (exit, output) => // list делает health-check → даём время
            {
                // Имя в выводе → точно настроен (даже если health-check вернул non-zero). Иначе: exit 0 и
                // нет имени → не настроен; не нашли claude/ошибка → не знаем (показываем кнопку Add).
                bool? configured = output.Contains(name) ? true : (exit == 0 ? false : (bool?)null);
                RenderMcpArea(configured);
            });
        }

        // claude на ФОНОВОМ потоке (не блокирует main/dispatcher); коллбэк маршалится в Update (главный поток)
        // через потокобезопасную очередь — НЕ трогаем EditorApplication.* с фонового потока.
        void RunClaudeAsync(string args, int timeoutMs, System.Action<int, string> done)
        {
            System.Threading.Tasks.Task.Run(() =>
            {
                var (exit, output) = RunClaude(args, timeoutMs);
                _mainQueue.Enqueue(() => done(exit, output));
            });
        }

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

        static (int exit, string output) RunClaude(string args, int timeoutMs)
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
                    if (!p.WaitForExit(timeoutMs))
                    {
                        try { p.Kill(); } catch { }
                        return (-1, "timeout");
                    }
                    var outp = p.StandardOutput.ReadToEnd();
                    var err = p.StandardError.ReadToEnd();
                    return (p.ExitCode, (outp + err).Trim());
                }
            }
            catch (System.Exception e)
            {
                return (-1, e.Message);
            }
        }

        // ── helpers ──────────────────────────────────────────────────────────

        static void BindToggle(VisualElement root, string label, bool initial, System.Action<bool> set, string tooltip = null)
        {
            var t = new Toggle(label) { value = initial };
            if (tooltip != null)
            {
                t.tooltip = tooltip;
            }
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

        static void SetDomainReloadOff(bool off)
        {
            if (off)
            {
                EditorSettings.enterPlayModeOptionsEnabled = true;
                EditorSettings.enterPlayModeOptions |= EnterPlayModeOptions.DisableDomainReload;
            }
            else
            {
                EditorSettings.enterPlayModeOptions &= ~EnterPlayModeOptions.DisableDomainReload;
            }
        }

        // ── refresh ──────────────────────────────────────────────────────────

        void Update()
        {
            while (_mainQueue.TryDequeue(out var a)) // результаты async-claude — на главном потоке
            {
                try { a(); } catch (System.Exception e) { UnityEngine.Debug.LogWarning("[Shtl MCP] " + e.Message); }
            }
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
            _status.text = "server: " + (up ? "● running" : "○ stopped") + "   " + s.ServerName + "   :" + s.Port;

            double age = s.LastRequestAgeSeconds;
            _client.text = "LLM client: " + (
                age < 0 ? "○ none yet (waiting for first call)" :
                age < 30 ? $"● active ({age:0}s ago)" :
                $"○ idle ({age:0}s since last call)");

            _identity.text = "project: " + Application.productName;
            _mode.text = "mode: " + (EditorApplication.isPlaying ? "PLAY" : "EDIT");
            if (_reloadDomainStatus != null)
            {
                _reloadDomainStatus.text = "Reload Domain on Enter Play: " + (DomainReloadOff() ? "OFF" : "ON");
            }
        }
    }
}
