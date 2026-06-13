---
entity: m1-server
content_class: code
compiled_at_commit: 799bf46
epoch: 001
status: active
needs_review: false
---

# Reference: M1 server (код)

Карта кода вехи M1. Пакет: `Packages/com.shtl.mcp/` (embedded UPM), один Editor-asmdef
`ShtlMcp.Editor` (логический split по папкам; физический 6-asmdef split — M2).
Реализует `wiki/systems/architecture.md` в объёме walking skeleton.

Запуск: `[InitializeOnLoad]` `ShtlMcp.Lifecycle.ShtlMcpBootstrap` → `delayCall` →
`ShtlMcpServer.Instance.EnsureStarted()`.

## Модули (namespace → публичный контракт)

| Namespace | Тип | Контракт |
|---|---|---|
| `ShtlMcp.Common` | `Fnv` | `Hash32(string)->uint`, `Hash4(string)->string` (FNV-1a, детерминирован между процессами) |
| | `PortAllocator` | `Base=9700`, `Range=100`, `Preferred(path)->int`, `Allocate(path, Func<int,bool> isFree)->int` |
| | `ServerName` | `Resolve(product, path, Func<string,string> livePathForName)->string`; дедуп `-<hash4>` при коллизии живого имени с другим путём |
| `ShtlMcp.Registry` | `InstanceEntry` | поля: ProjectName/ProjectPath/UnityVersion/ServerName/Port/Pid/Mode/Compiling/StartedAt/LastHeartbeat |
| | `RegistryStore` | `Read()`, `Upsert(e)` (atomic temp+rename), `Remove(path)`, `LivePathForName(name, ttl)`, static `Prune(list, now, ttl)`; файл `~/.unity-mcp/registry.json`, camelCase |
| `ShtlMcp.Dispatch` | `MainThreadDispatcher` | `Enqueue(Action)`, `Drain()` (главный поток), `RunOnMain<T>(Func<T>, timeoutMs)` (фоновый поток ждёт результат/исключение) |
| `ShtlMcp.Logging` | `LogLevel`/`LogItem`/`LogBuffer` | потокобезопасный ring (`Add`, `Get(min, count)`) |
| `ShtlMcp.Transport` | `JsonRpc` | `Result(id, token)`, `Error(id, code, msg)` |
| | `IToolInvoker` | `ListTools()->JArray`, `Invoke(name, args)->JObject` |
| | `McpRouter` | чистый `Handle(json)->json`: `initialize`/`notifications/initialized`/`tools/list`/`tools/call`; protocolVersion `2024-11-05`; ошибки -32700/-32601 |
| `ShtlMcp.Tools` | `ITool` | `Name/Description/InputSchema/NeedsMainThread/Invoke(args)` |
| | `IEditorContext` | 10 свойств идентичности/режима |
| | `ToolRegistry` | `Register/Get/List` |
| | `StatusTool` | `status` — идентичность+режим+health (через `IEditorContext`) |
| | `GetLogsTool` | `get_logs` — последние логи из `LogBuffer`, фильтр `minLevel`/`count` |
| `ShtlMcp.Server` | `HttpServer` | фоновый `HttpListener` на `http://127.0.0.1:<port>/`, POST→`Handle`; `Start/Stop/IsListening` (без Unity API) |
| | `DispatchingToolInvoker` | `IToolInvoker`, маршалит в главный поток когда `NeedsMainThread` (timeout 5000мс) |
| `ShtlMcp.Lifecycle` | `ShtlMcpConfig` | `Enabled` (EditorPrefs, default true) |
| | `EditorContext` | `IEditorContext` поверх `Application`/`EditorApplication`/`Process` |
| | `ShtlMcpServer` | singleton-фасад: `EnsureStarted/StopListenerForReload/RestartNow/WatchdogTick`, `IsListening/Port/ServerName`; порт в `SessionState`, heartbeat в реестр, подписка `logMessageReceivedThreaded`, drain диспетчера на `EditorApplication.update` |
| | `ShtlMcpBootstrap` | `[InitializeOnLoad]`: старт + `AssemblyReloadEvents.beforeAssemblyReload`→Stop + watchdog (1с tick: respawn + heartbeat) |
| `ShtlMcp.UI` | `DashboardWindow` | `EditorWindow` (UI Toolkit), меню `Window/Shtl MCP`: статус, строка `claude mcp add` + Copy, Restart |

## Подключение
1. `cat ~/.unity-mcp/registry.json` → порт инстанса.
2. `claude mcp add --transport http unity-<project> http://127.0.0.1:<port>/mcp`.
3. Инструменты: `mcp__unity-<project>__status`, `..._get_logs`.

## Тесты
`Packages/com.shtl.mcp/Tests/Editor/` — 34 EditMode-теста (NUnit), чистая логика:
Fnv 3, Port 3, ServerName 4, Registry 4, Dispatcher 4, LogBuffer 3, JsonRpc 2,
McpRouter 7, Status 2, GetLogs 2. Прогон — `wiki/tasks/m1-walking-skeleton/PLAN.md`
(«Соглашения по запуску тестов»).
