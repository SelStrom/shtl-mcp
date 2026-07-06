using System;
using System.Net;
using System.Net.Sockets;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEditor.Compilation;
using UnityEngine;
using Shtl.Mcp.Common;
using Shtl.Mcp.Dispatch;
using Shtl.Mcp.Jobs;
using Shtl.Mcp.Logging;
using Shtl.Mcp.Registry;
using Shtl.Mcp.Server;
using Shtl.Mcp.Tools;
using Shtl.Mcp.Transport;

namespace Shtl.Mcp.Lifecycle
{
    public sealed class ShtlMcpServer
    {
        static ShtlMcpServer _instance;
        public static ShtlMcpServer Instance => _instance ?? (_instance = new ShtlMcpServer());

        const string PortKey = "Shtl.Mcp.Port";
        const string StartedKey = "Shtl.Mcp.StartedTicks";
        const string ReloadCountKey = "Shtl.Mcp.ReloadCount";
        static readonly TimeSpan Ttl = TimeSpan.FromSeconds(30);
        static readonly string RegistryPath = System.IO.Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".unity-mcp", "registry.json");
        // Версия пакета из package.json; null — если исходники живут в Assets/ вне UPM-пакета.
        static readonly string PackageVersion = UnityEditor.PackageManager.PackageInfo
            .FindForAssembly(typeof(ShtlMcpServer).Assembly)?.version ?? "unknown";

        readonly MainThreadDispatcher _dispatcher = new MainThreadDispatcher();
        readonly LogBuffer _logs = new LogBuffer(500);
        readonly JobStore _jobs = new JobStore();
        readonly ToolRegistry _tools = new ToolRegistry();
        readonly RegistryStore _registry = new RegistryStore(RegistryPath);
        readonly CallTail _calls = new CallTail(20); // F5/AC5.5: хвост последних вызовов для дашборда

        volatile HttpServer _http; // читается фоновым watchdog'ом → volatile
        bool _customToolsDiscovered; // F3/AC3.6: дискавери кастомных — один раз на инстанс (не спамить на bind-retry)
        volatile string _serverName; // читается фоновым watchdog'ом → volatile
        DateTime _lastRequestUtc = DateTime.MinValue;
        DateTime _listenerStartedUtc = DateTime.MinValue;

        public int Port { get; private set; }
        public string ServerName => _serverName;
        public bool IsListening => _http != null && _http.IsListening;

        // Секунд с последнего HTTP-запроса (живой коннект клиента-LLM); -1 = запросов ещё не было.
        public double LastRequestAgeSeconds =>
            _lastRequestUtc == DateTime.MinValue ? -1 : (DateTime.UtcNow - _lastRequestUtc).TotalSeconds;

        // F5/AC5.5: хвост последних MCP-вызовов (новейшие-первыми) для дашборда.
        public CallTail.Entry[] RecentCalls() => _calls.Snapshot();

        string ProjectPath => System.IO.Directory.GetParent(Application.dataPath).FullName;
        double Uptime => (DateTime.UtcNow - new DateTime(long.Parse(
            SessionState.GetString(StartedKey, DateTime.UtcNow.Ticks.ToString())), DateTimeKind.Utc)).TotalSeconds;

        // Время жизни текущего listener-инстанса: сбрасывается при каждом re-spawn (in-domain, не persisted).
        double ListenerUptime => _listenerStartedUtc == DateTime.MinValue
            ? 0
            : (DateTime.UtcNow - _listenerStartedUtc).TotalSeconds;

        // Durable-счётчик пережитых domain reload за сессию Unity (SessionState).
        int ReloadCount => SessionState.GetInt(ReloadCountKey, 0);

        /// Инкремент durable reloadCount. Вызывается из beforeAssemblyReload (до StopListenerForReload).
        public void NoteReloadStarting() => SessionState.SetInt(ReloadCountKey, SessionState.GetInt(ReloadCountKey, 0) + 1);

        public void EnsureStarted()
        {
            if (IsListening)
            {
                return;
            }

            if (SessionState.GetString(StartedKey, "") == "")
            {
                SessionState.SetString(StartedKey, DateTime.UtcNow.Ticks.ToString());
            }

            Port = ResolvePort();
            _serverName = Shtl.Mcp.Common.ServerName.Resolve(Application.productName, ProjectPath,
                name => _registry.LivePathForName(name, Ttl));

            Application.logMessageReceivedThreaded -= OnLog;
            Application.logMessageReceivedThreaded += OnLog;

            EditorApplication.update -= _dispatcher.Drain;
            EditorApplication.update += _dispatcher.Drain;

            var ctx = new EditorContext(() => Port, () => _serverName, () => Uptime, ClientCount,
                () => ListenerUptime, () => ReloadCount);
            _tools.Register(new StatusTool(ctx));
            _tools.Register(new GetLogsTool(_logs));
            _tools.Register(new PingTool(() => _dispatcher.LastDrainUtc, () => ListenerUptime)); // bg-liveness (AC4.8)
            _tools.Register(new RecompileTool(_jobs, () => ReloadCount));
            _tools.Register(new SetPlayModeTool(_jobs, () => ReloadCount));
            _tools.Register(new GetJobTool(_jobs));
            _tools.Register(new RunTestsTool(_jobs));
            _tools.Register(new ClearLogsTool(_logs));
            _tools.Register(new RefreshAssetsTool(_jobs, () => ReloadCount));
            _tools.Register(new FindAssetsTool());
            _tools.Register(new ReadAssetTool());
            _tools.Register(new WriteAssetTool(_jobs, () => ReloadCount));
            _tools.Register(new MoveAssetTool());
            _tools.Register(new DeleteAssetTool());
            _tools.Register(new CreateFolderTool());
            _tools.Register(new CreatePrefabTool());
            _tools.Register(new OpenPrefabTool());
            _tools.Register(new SavePrefabTool());
            _tools.Register(new ClosePrefabTool());
            _tools.Register(new InstantiatePrefabTool());
            _tools.Register(new GetHierarchyTool());
            _tools.Register(new FindGameObjectTool());
            _tools.Register(new GameObjectCreateTool());
            _tools.Register(new GameObjectModifyTool());
            _tools.Register(new GameObjectDestroyTool());
            _tools.Register(new SetParentTool());
            _tools.Register(new AddComponentTool());
            _tools.Register(new RemoveComponentTool());
            _tools.Register(new GetObjectTool());
            _tools.Register(new ModifyObjectTool());
            _tools.Register(new OpenSceneTool());
            _tools.Register(new SaveSceneTool());
            _tools.Register(new ListScenesTool());
            _tools.Register(new CreateSceneTool());
            _tools.Register(new UnloadSceneTool());
            _tools.Register(new SetActiveSceneTool());
            _tools.Register(new GetSelectionTool());
            _tools.Register(new SetSelectionTool());
            _tools.Register(new ScreenshotTool());
            _tools.Register(new ScreenshotUxmlTool());
            _tools.Register(new GetConfigTool(ConfigSnapshot));
            _tools.Register(new ExecuteMenuItemTool());
            _tools.Register(new RunCsharpTool(() => ShtlMcpConfig.AllowRunCsharp));
            _tools.Register(new CallMethodTool());
            _tools.Register(new FindMethodTool());
            // F3/AC3.6: кастомные тулы хоста ([McpTool]) — ПОСЛЕ встроенных. Один раз на инстанс: на bind-retry
            // (порт занят) EnsureStarted перезапускается тем же инстансом, а _tools уже содержит кастомные →
            // повторный дискавери спамил бы «имя занято». Новый инстанс (domain reload) сбросит флаг и пере-обнаружит.
            if (!_customToolsDiscovered)
            {
                ToolDiscovery.DiscoverAndRegister(_tools);
                _customToolsDiscovered = true;
            }
            TestRunCallbacks.ReattachIfPending(_jobs); // переподписка на in-flight прогон после domain reload

            // reload-job (recompile/set_play_mode) финализируются после reload: копим ошибки компиляции
            // и завершаем set_play_mode по достижению режима. Подписки переустанавливаются на каждой загрузке.
            CompilationPipeline.assemblyCompilationFinished -= ReloadJobs.OnAssemblyCompiled;
            CompilationPipeline.assemblyCompilationFinished += ReloadJobs.OnAssemblyCompiled;
            EditorApplication.playModeStateChanged -= OnPlayModeChanged;
            EditorApplication.playModeStateChanged += OnPlayModeChanged;
            // Восстановить no-throttle: in-flight прогон → держим full-rate; иначе откатить осиротевший
            // снимок (краш в середине прогона оставил редактор в no-throttle, см. TestRunnerNoThrottle).
            bool runPending = !string.IsNullOrEmpty(SessionState.GetString(RunTestsTool.JobMarkerKey, ""));
            TestRunnerNoThrottle.RecoverOnLoad(runPending);
            PlayModeOptionsGuard.RecoverOnLoad(runPending); // PlayMode-прогон: держим DisableDomainReload через reload

            var invoker = new DispatchingToolInvoker(_tools, _dispatcher);
            var info = new ServerInfo
            {
                Name = _serverName,
                Version = PackageVersion,
                Instructions = "Unity MCP server embedded in the Unity Editor. If a tool call fails " +
                    "(connection refused/timeout), read ~/.unity-mcp/registry.json — each instance entry has " +
                    "a 'recovery' block with steps and a restart command. The 'ping' tool answers even when the " +
                    "main thread is blocked (modal dialog / compiling), so it tells a wedged main thread from a dead server."
            };
            var router = new McpRouter(invoker, info, Application.productName, // INV-3 + F7/AC7.3
                "unreachable? read " + RegistryPath + " — each entry has a 'recovery' block (steps + restart command)",
                _calls.Record); // F5/AC5.5: писать хвост вызовов

            _http = new HttpServer(Port, router.Handle, () => _lastRequestUtc = DateTime.UtcNow);
            _http.Start();
            // bind мог не удаться (порт ещё в TIME_WAIT) — _http НЕ обнуляем: объект с готовым router'ом
            // остаётся, фоновый watchdog повторит _http.Start() до успеха (не зависит от update-loop).
            if (_http.IsListening)
            {
                _listenerStartedUtc = DateTime.UtcNow; // отметка re-spawn для listenerUptimeSeconds
                Heartbeat();
            }
            IdleKeepAlive.Reconcile(ShtlMcpConfig.Enabled && ShtlMcpConfig.IdleKeepAlive); // AC4.10: применить сразу на старте/после reload
        }

        /// AC4.10: применить idle-keepalive немедленно (зовётся дашбордом после смены тогла).
        public void SyncKeepAlive() => IdleKeepAlive.Reconcile(ShtlMcpConfig.Enabled && ShtlMcpConfig.IdleKeepAlive);

        public void StopListenerForReload()
        {
            _http?.Abort();
            _http = null;
        }

        public void RestartNow()
        {
            StopListenerForReload();
            EnsureStarted();
        }

        /// Фоново-безопасный ре-бинд: только HttpListener.Start (без main-thread API). Смену ПОРТА здесь
        /// НЕ делаем — это registry-aware ResolvePort на главном потоке (EnsureStarted). Тут лишь поднимаем
        /// listener на уже выбранном порту. Возвращает текущее состояние прослушивания.
        internal bool TryReBindListener()
        {
            var http = _http;
            if (http == null)
            {
                return false; // ещё не сконструирован (до первого EnsureStarted) — поднимет main
            }
            if (!http.IsListening)
            {
                http.Start();
                if (http.IsListening)
                {
                    _listenerStartedUtc = DateTime.UtcNow;
                }
            }
            return http.IsListening;
        }

        public void WatchdogTick()
        {
            // AC4.10: держать full-rate update в фоне (или отпустить, если выключено) — ДО enabled-гейта,
            // чтобы при выключенном сервере троттлинг вернулся к Default.
            IdleKeepAlive.Reconcile(ShtlMcpConfig.Enabled && ShtlMcpConfig.IdleKeepAlive);
            if (!ShtlMcpConfig.Enabled)
            {
                StopListenerForReload();
                return;
            }

            if (!IsListening)
            {
                EnsureStarted();
            }

            CheckControlFlag(); // LLM-инициируемый форс-рестарт через ~/.unity-mcp/<serverName>.cmd (AC2.6)
            RunTestsTool.SweepOrphan(_jobs); // авто-fail зависшего/осиротевшего тест-прогона + вернуть троттлинг
            ReloadJobs.FinalizeOnTick(_jobs, ReloadCount); // завершить recompile/set_play_mode job после reload
            Heartbeat();
            // Повторно ПОСЛЕ SweepOrphan: если он снял маркер прогона и откатил no-throttle к Default, keepalive
            // переустанавливается в тот же тик (а не на следующем), закрывая 1-tick задержку re-assert (AC4.10).
            IdleKeepAlive.Reconcile(ShtlMcpConfig.Enabled && ShtlMcpConfig.IdleKeepAlive);
        }

        // Завершение set_play_mode-job по достижению целевого режима (подписка переживает reload).
        void OnPlayModeChanged(PlayModeStateChange change) => ReloadJobs.OnPlayModeChanged(_jobs, change);

        // Control-channel (INV-5/F2 §4): модель пишет `~/.unity-mcp/<serverName>.cmd`, watchdog исполняет.
        // Работает, когда HTTP-листенер завис, но главный поток тикает. Чтение+удаление атомарно (один тик
        // → не исполнить дважды). Канал-файл, как реестр — ноль внешних процессов.
        void CheckControlFlag()
        {
            if (string.IsNullOrEmpty(_serverName))
            {
                return;
            }
            var cmdPath = System.IO.Path.Combine(System.IO.Path.GetDirectoryName(RegistryPath), _serverName + ".cmd");
            if (!System.IO.File.Exists(cmdPath))
            {
                return;
            }
            string cmd;
            try
            {
                cmd = System.IO.File.ReadAllText(cmdPath).Trim();
                System.IO.File.Delete(cmdPath); // read+delete за один тик
            }
            catch
            {
                return;
            }
            if (cmd == "restart")
            {
                RestartNow();
            }
        }

        // Снимок конфига для get_config (строится на главном потоке, читает EditorPrefs).
        JObject ConfigSnapshot() => new JObject
        {
            ["enabled"] = ShtlMcpConfig.Enabled,
            ["portRangeStart"] = ShtlMcpConfig.PortRangeStart,
            ["portRangeCount"] = ShtlMcpConfig.PortRangeCount,
            ["heartbeatSeconds"] = ShtlMcpConfig.HeartbeatSeconds,
            ["allowRunCsharp"] = ShtlMcpConfig.AllowRunCsharp,
            ["idleKeepAlive"] = ShtlMcpConfig.IdleKeepAlive, // AC4.10: видна ли keepalive-настройка через get_config (диагностика фон-затыка)
            ["port"] = Port,
            ["serverName"] = _serverName
        };

        int ClientCount() => (DateTime.UtcNow - _lastRequestUtc) < TimeSpan.FromSeconds(30) ? 1 : 0;

        int ResolvePort()
        {
            int saved = SessionState.GetInt(PortKey, 0);
            if (saved != 0)
            {
                return saved;
            }

            int port = PortAllocator.Allocate(ProjectPath, IsPortFree,
                ShtlMcpConfig.PortRangeStart, ShtlMcpConfig.PortRangeCount);
            SessionState.SetInt(PortKey, port);
            return port;
        }

        static bool IsPortFree(int port)
        {
            try
            {
                var l = new TcpListener(IPAddress.Loopback, port);
                l.Start(); l.Stop(); return true;
            }
            catch (SocketException) { return false; }
        }

        // Самоописываемый recovery-блок для registry.json (F7/AC7.1) — модель читает его, когда MCP-канал мёртв.
        RecoveryInfo BuildRecovery(int pid)
        {
            var cmdPath = System.IO.Path.Combine(System.IO.Path.GetDirectoryName(RegistryPath), (_serverName ?? "") + ".cmd");
            return new RecoveryInfo
            {
                ControlFlagPath = cmdPath,
                RegistryPath = RegistryPath,
                Steps = new[]
                {
                    "MCP call failed (connection refused/timeout)? This registry.json names the instance (pid, port).",
                    "Is Unity alive? `kill -0 " + pid + "` (or `ps -p " + pid + "`).",
                    "Alive but listener wedged → write 'restart' to controlFlagPath, wait ~2s, reconnect MCP, retry.",
                    "pid dead (Unity closed/crashed) → a human must reopen Unity (out of MCP scope).",
                    "`ping` answers but status/other main-thread tools time out → main thread is blocked " +
                    "(modal dialog / compiling); a human may need to dismiss the modal or focus the Unity window."
                },
                RestartCommand = "printf 'restart' > '" + cmdPath + "'"
            };
        }

        void OnLog(string message, string stack, LogType type)
            => _logs.Add(message, stack,
                type == LogType.Error || type == LogType.Exception || type == LogType.Assert ? LogLevel.Error :
                type == LogType.Warning ? LogLevel.Warning : LogLevel.Info);

        void Heartbeat()
        {
            try
            {
                int pid = System.Diagnostics.Process.GetCurrentProcess().Id;
                _registry.Upsert(new InstanceEntry
                {
                    ProjectName = Application.productName,
                    ProjectPath = ProjectPath,
                    UnityVersion = Application.unityVersion,
                    ServerName = _serverName,
                    Port = Port,
                    Pid = pid,
                    Mode = EditorApplication.isPlaying ? "play" : "edit",
                    Compiling = EditorApplication.isCompiling,
                    StartedAt = new DateTime(long.Parse(SessionState.GetString(StartedKey,
                        DateTime.UtcNow.Ticks.ToString())), DateTimeKind.Utc),
                    LastHeartbeat = DateTime.UtcNow,
                    Recovery = BuildRecovery(pid) // F7/AC7.1: durable самоописываемое восстановление
                });
            }
            catch { }
        }
    }
}
