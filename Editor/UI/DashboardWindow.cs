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
        Foldout _callsFoldout;       // AC5.5: хвост последних вызовов
        VisualElement _callsList;
        VisualElement _bcArea;       // AC7.4: opt-in host-крошка
        VisualElement _warnArea;     // AC5.4: ⚠ Reload Domain on Play — виден только когда включён
        Toggle _noReload;
        double _next;
        long _callsSig = -1;         // AC5.5: «подпись» хвоста — UI трогается только при изменении состава

        const float SectionGap = 8;  // единый вертикальный отступ между блоками дашборда

        // Фоновый поток (claude) кладёт сюда коллбэк, главный поток (Update) исполняет. EditorApplication.*
        // нельзя трогать с фонового потока — это и была ошибка в консоли.
        readonly System.Collections.Concurrent.ConcurrentQueue<System.Action> _mainQueue
            = new System.Collections.Concurrent.ConcurrentQueue<System.Action>();

        public void CreateGUI()
        {
            minSize = new Vector2(320, 240);
            var root = rootVisualElement;
            root.style.paddingLeft = root.style.paddingTop = root.style.paddingRight = root.style.paddingBottom = 8;

            // Контент в ScrollView: окно меньше контента → скролл, а не молчаливый клип.
            var scroll = new ScrollView { style = { flexGrow = 1 } };
            root.Add(scroll);

            _status = new Label();   // своё состояние сервера
            _client = new Label();   // живой коннект LLM-клиента
            _identity = new Label();
            _mode = new Label();
            scroll.Add(_status);
            scroll.Add(_client);
            scroll.Add(_identity);
            scroll.Add(_mode);

            // Подключение к Claude Code: одна кнопка; если инстанс уже добавлен — прячем кнопку И manual.
            // Manual — часть этого же блока (без отступа), скрывается/показывается вместе с кнопкой.
            _mcpArea = new VisualElement { style = { marginTop = SectionGap } };
            scroll.Add(_mcpArea);

            // Manual fallback (свёрнут): готовая команда. Скрывается вместе с кнопкой Add, когда настроено.
            _manual = new Foldout { text = "Manual add command", value = false };
            Indent(_manual);
            var cmd = new TextField { isReadOnly = true, multiline = true, value = AddCommand() };
            _manual.Add(cmd);
            _manual.Add(new Button(() => EditorGUIUtility.systemCopyBuffer = cmd.value) { text = "Copy" });
            scroll.Add(_manual);

            RenderMcpArea(null); // null = «проверяем…» (после создания _manual)
            CheckConfiguredAsync();

            // AC5.4: предупреждение в основной области, видно только пока Reload Domain on Play включён.
            _warnArea = new VisualElement { style = { flexDirection = FlexDirection.Row, alignItems = Align.Center, marginTop = SectionGap } };
            _warnArea.Add(new Label("⚠ Reload Domain on Play: ON — every Play enter kills the MCP listener for seconds")
            {
                style = { color = new Color(0.9f, 0.72f, 0.3f), whiteSpace = WhiteSpace.Normal, flexGrow = 1, flexShrink = 1 }
            });
            _warnArea.Add(new Button(() => { SetDomainReloadOff(true); Refresh(); }) { text = "Fix" });
            scroll.Add(_warnArea);

            // Хвост последних MCP-вызовов (AC5.5): метод, ✓/✗, длительность, время.
            _callsFoldout = new Foldout { text = "Recent calls", value = true, style = { marginTop = SectionGap } };
            Indent(_callsFoldout);
            _callsList = new VisualElement();
            _callsFoldout.Add(_callsList);
            scroll.Add(_callsFoldout);

            // Host recovery breadcrumb (AC7.4) — opt-in, свёрнут. Запись только по явному подтверждению.
            var bc = new Foldout { text = "Host recovery breadcrumb", value = false, style = { marginTop = SectionGap } };
            Indent(bc);
            _bcArea = new VisualElement();
            bc.Add(_bcArea);
            scroll.Add(bc);
            RenderBreadcrumb();

            // Настройки — в foldout, свёрнуты (дашборд компактный).
            var settings = new Foldout { text = "Settings", value = false, style = { marginTop = SectionGap } };
            Indent(settings);
            scroll.Add(settings);

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
            _noReload = noReload;

            BindToggle(settings, "Server enabled (auto-start)", ShtlMcpConfig.Enabled,
                v =>
                {
                    ShtlMcpConfig.Enabled = v;
                    ShtlMcpServer.Instance.SyncKeepAlive(); // выключение сервера → сразу отпустить keepalive-no-throttle
                });
            BindToggle(settings, "Allow run_csharp  ⚠ FOOTGUN", ShtlMcpConfig.AllowRunCsharp, v => ShtlMcpConfig.AllowRunCsharp = v,
                "Разрешает компиляцию+исполнение произвольного Editor-C# через run_csharp. Опасно: полный " +
                "доступ к проекту/ФС. По умолчанию выкл; включай только осознанно и выключай после.");
            BindInt(settings, "Port range start", ShtlMcpConfig.PortRangeStart, v => ShtlMcpConfig.PortRangeStart = v);
            BindInt(settings, "Port range count", ShtlMcpConfig.PortRangeCount, v => ShtlMcpConfig.PortRangeCount = v);
            BindInt(settings, "Heartbeat seconds", ShtlMcpConfig.HeartbeatSeconds, v => ShtlMcpConfig.HeartbeatSeconds = v);
            BindToggle(settings, "Keep editor awake while server on (idle keepalive)", ShtlMcpConfig.IdleKeepAlive,
                v =>
                {
                    ShtlMcpConfig.IdleKeepAlive = v;
                    ShtlMcpServer.Instance.SyncKeepAlive();
                },
                "Зачем: в фоне (окно Unity не в фокусе + простой) Unity троттлит editor update → главный поток " +
                "почти не тикает → main-thread MCP-инструменты виснут, и даже .cmd-рестарт не срабатывает (оба " +
                "висят на том же тике). Этот тогл держит редактор в No-Throttling, пока сервер включён → MCP " +
                "и control-flag остаются доступны в фоне.\n\nКомпромисс: редактор не «засыпает» в фоне → выше " +
                "idle-CPU/расход батареи. Best-effort: подавление фонового троттла версионно-зависимо (Unity " +
                "LTS) — источник истины о затыке остаётся инструмент ping. Default OFF.");
            // AC5.6: рестарт — постоянное действие дашборда (как в макете), не спрятан в Settings.
            scroll.Add(new Button(() => ShtlMcpServer.Instance.RestartNow())
            {
                text = "Restart server",
                style = { marginTop = SectionGap, alignSelf = Align.FlexEnd }
            });

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

        // `claude mcp list` печатает по строке на сервер: «<name>: <url> …». Матчим имя как токен в начале
        // строки, иначе инстанс 'unity-foo' ложно засчитал бы дедуп-соседа 'unity-foo-ab12cd34' (подстрока).
        static bool NameListed(string listOutput, string name)
        {
            if (string.IsNullOrEmpty(listOutput) || string.IsNullOrEmpty(name))
            {
                return false;
            }
            var pattern = @"(?m)^\s*" + System.Text.RegularExpressions.Regex.Escape(name) + @"\s*:";
            return System.Text.RegularExpressions.Regex.IsMatch(listOutput, pattern);
        }

        void CheckConfiguredAsync()
        {
            var name = ShtlMcpServer.Instance.ServerName;
            RunClaudeAsync("mcp list", 8000, (exit, output) => // list делает health-check → даём время
            {
                // Имя в выводе → точно настроен (даже если health-check вернул non-zero). Иначе: exit 0 и
                // нет имени → не настроен; не нашли claude/ошибка → не знаем (показываем кнопку Add).
                bool? configured = NameListed(output, name) ? true : (exit == 0 ? false : (bool?)null);
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
                    // Читать пайпы ДО ожидания выхода: дочерний процесс с выводом больше буфера пайпа
                    // блокируется на write и никогда не выйдет (взаимная блокировка с WaitForExit).
                    var outTask = p.StandardOutput.ReadToEndAsync();
                    var errTask = p.StandardError.ReadToEndAsync();
                    if (!p.WaitForExit(timeoutMs))
                    {
                        try { p.Kill(); } catch { }
                        return (-1, "timeout");
                    }
                    // процесс вышел → стримы закрываются, ReadToEndAsync довершается (мы на фоновом потоке)
                    return (p.ExitCode, (outTask.Result + errTask.Result).Trim());
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

            // Без секундного счётчика: текст меняется только при смене состояния (none/active/idle) —
            // дефолтный дашборд ничего не перерисовывает по тику.
            double age = s.LastRequestAgeSeconds;
            _client.text = "LLM client: " + (
                age < 0 ? "○ none yet (waiting for first call)" :
                age < 30 ? "● active" :
                "○ idle");

            _identity.text = "project: " + Application.productName;
            _mode.text = "mode: " + (EditorApplication.isPlaying ? "PLAY" : "EDIT");
            bool reloadOff = DomainReloadOff();
            if (_warnArea != null)
            {
                _warnArea.style.display = reloadOff ? DisplayStyle.None : DisplayStyle.Flex;
            }
            // Fix-кнопка/правка настроек снаружи → тогл в Settings не должен рассинхронизироваться
            _noReload?.SetValueWithoutNotify(reloadOff);
            RenderCalls();
        }

        // AC5.5: хвост последних вызовов (новейшие сверху) колонками: ✓/✗ | метод (ellipsis) | ms | HH:mm:ss.
        // Время абсолютное (как в макете) → строки статичны: UI трогается ТОЛЬКО при изменении состава
        // вызовов, в остальное время тик не перерисовывает ничего.
        void RenderCalls()
        {
            if (_callsList == null)
            {
                return;
            }
            var calls = ShtlMcpServer.Instance.RecentCalls();
            long sig = calls.Length == 0 ? 0 : calls[0].AtTicks ^ ((long)calls.Length << 56);
            if (sig == _callsSig)
            {
                return;
            }
            _callsSig = sig;
            _callsFoldout.text = "Recent calls (" + calls.Length + ")";
            _callsList.Clear();
            if (calls.Length == 0)
            {
                _callsList.Add(new Label("— none yet —") { style = { opacity = 0.6f } });
                return;
            }
            foreach (var c in calls)
            {
                var row = new VisualElement { style = { flexDirection = FlexDirection.Row } };
                var mark = new Label(c.Ok ? "✓" : "✗") { style = { width = 16, flexShrink = 0 } };
                var method = new Label(c.Method)
                {
                    tooltip = c.Method, // узкое окно клипает имя (ellipsis) → полное имя в тултипе
                    style =
                    {
                        flexGrow = 1, flexShrink = 1,
                        overflow = Overflow.Hidden,
                        textOverflow = TextOverflow.Ellipsis,
                        whiteSpace = WhiteSpace.NoWrap
                    }
                };
                var ms = new Label(c.Ms.ToString("0") + " ms")
                {
                    style = { width = 56, flexShrink = 0, unityTextAlign = TextAnchor.MiddleRight, opacity = 0.8f }
                };
                var at = new Label(TimeStr(c.AtTicks))
                {
                    style = { width = 64, flexShrink = 0, unityTextAlign = TextAnchor.MiddleRight, opacity = 0.7f }
                };
                if (!c.Ok)
                {
                    var err = new Color(0.85f, 0.5f, 0.4f);
                    mark.style.color = err;
                    method.style.color = err;
                }
                row.Add(mark);
                row.Add(method);
                row.Add(ms);
                row.Add(at);
                _callsList.Add(row);
            }
        }

        static string TimeStr(long atTicks)
            => new System.DateTime(atTicks, System.DateTimeKind.Utc).ToLocalTime().ToString("HH:mm:ss");

        // ── AC7.4: opt-in host-крошка ────────────────────────────────────────

        static string HostProjectRoot() => System.IO.Directory.GetParent(Application.dataPath).FullName;

        void RenderBreadcrumb()
        {
            if (_bcArea == null)
            {
                return;
            }
            _bcArea.Clear();
            var target = HostBreadcrumb.TargetPath(HostProjectRoot());
            bool present = System.IO.File.Exists(target) && HostBreadcrumb.IsPresent(System.IO.File.ReadAllText(target));

            var why = new Label("Opt-in: append a one-line recovery pointer to the host project's CLAUDE.md so a " +
                "fresh LLM session is primed even if this server is already dead (cold-start). Default: nothing (INV-2).");
            why.style.whiteSpace = WhiteSpace.Normal;
            why.style.opacity = 0.8f;
            _bcArea.Add(why);
            _bcArea.Add(new Label("Target: " + target) { style = { opacity = 0.7f, marginTop = 2 } });

            if (present)
            {
                _bcArea.Add(new Label("✓ already present") { style = { color = new Color(0.4f, 0.8f, 0.4f), marginTop = 2 } });
                return;
            }
            var preview = new TextField { isReadOnly = true, multiline = true, value = HostBreadcrumb.Text() };
            preview.style.marginTop = 4;
            _bcArea.Add(preview);
            _bcArea.Add(new Button(OnAddBreadcrumb) { text = "Add to host CLAUDE.md", style = { marginTop = 2 } });
        }

        void OnAddBreadcrumb()
        {
            var target = HostBreadcrumb.TargetPath(HostProjectRoot());
            // Человеко-инициированный модал (клик по кнопке) — это НЕ MCP-freeze (тот про автономные модалы).
            bool ok = EditorUtility.DisplayDialog("Add recovery breadcrumb?",
                "Append the shtl-mcp recovery pointer to:\n" + target +
                "\n\nThis edits the host project's CLAUDE.md. Proceed?", "Add", "Cancel");
            if (!ok)
            {
                return;
            }
            try
            {
                HostBreadcrumb.AddTo(target);
            }
            catch (System.Exception e)
            {
                UnityEngine.Debug.LogWarning("[Shtl MCP] breadcrumb write failed: " + e.Message);
            }
            RenderBreadcrumb();
        }
    }
}
