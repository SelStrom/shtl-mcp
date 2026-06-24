---
entity: architecture
content_class: intent-derived
source_refs:
  - raw/domain/overview.md
compiled_at_commit: pending
epoch: 001
status: active
needs_review: false
---

# Архитектура shtl-mcp

## Главная идея
MCP-сервер живёт **внутри Unity Editor** (Editor-сборка пакета). В отличие от всех
существующих Unity-MCP (трёхзвенная схема: AI-клиент ↔ внешний серверный процесс
Node/.NET ↔ плагин в Unity по WebSocket/SignalR) — здесь **нет внешнего процесса**.
Один инстанс Unity = один HTTP-сервер. Это прямое следствие INV-2 (самодостаточность).

```
Claude Code ──Streamable HTTP (JSON-RPC 2.0)──▶ HttpListener  (фоновый поток)
  mcp__unity-PerfectWar__*                            │ enqueue(command)
                                                      ▼
                                   MainThreadDispatcher (EditorApplication.update)
                                                      │ исполняет Editor API в гл. потоке
                                                      ▼
                                   Tool registry  →  Unity Editor API
                                                      │
  долгие/reload-команды ──▶ JobStore (SessionState) ──┘  (get_job для опроса)
```

## Транспорт
- Streamable HTTP, единственный эндпойнт `POST /mcp`.
- Минимальный MCP поверх JSON-RPC 2.0: `initialize`, `tools/list`, `tools/call`
  (опц. `resources/*` позже). SSE-стрим прогресса — опц. v2; для v1 достаточно
  request/response JSON.
- Реализация **ручная и минимальная**: официальный C# MCP SDK тянет ASP.NET Core
  и современный .NET — не лезет в Unity (.NET Standard 2.1 / Mono). Берём
  `System.Net.HttpListener` (BCL) + Newtonsoft.Json.

## Модель потоков (критично)
- `HttpListener` принимает запросы на **фоновом** потоке.
- Unity Editor API **не потокобезопасен** → любая работа с ним идёт через
  `MainThreadDispatcher`, который выполняет команды в `EditorApplication.update`
  (главный поток). Фоновый поток кладёт команду в очередь и ждёт результат
  (с таймаутом) либо сразу получает `jobId` для async-команд.
- Быстрые команды → синхронный ответ. Долгие/reload-триггерящие → async-job (см.
  `lifecycle-and-reload.md`).

## Стек и зависимости
- C# only; Unity 2022 LTS+; .NET Standard 2.1 / Mono.
- `System.Net.HttpListener` (BCL) — HTTP.
- Newtonsoft.Json (`com.unity.nuget.newtonsoft-json`) — JSON-RPC (Unity
  `JsonUtility` недостаточен для произвольного JSON-RPC).
- Без Node/Python, без ASP.NET Core, без нативных бинарей, без внешних процессов.

## Модульность (asmdef — изоляция и тестируемость)
Каждая сборка — одна ответственность, тестируется отдельно. **Реализовано** (таск `m2-asmdef-split`):
6 Editor-only asmdef под папками `Editor/<сборка>/`. namespace'ы исторические (`Shtl.Mcp.Dispatch/Jobs/
Logging/Common/Server` живут внутри новых сборок — папка ≠ namespace).

| asmdef | Ответственность | Содержит (namespace) | references |
|---|---|---|---|
| `Shtl.Mcp.Transport` | HttpListener + парсер/сериализатор JSON-RPC, маршрутизация MCP-методов | JsonRpc/ServerInfo, McpRouter, IToolInvoker (`Transport`); HttpServer (`Server`) | Newtonsoft |
| `Shtl.Mcp.Dispatcher` | Очередь команд, исполнение в главном потоке, JobStore (SessionState), LogBuffer | MainThreadDispatcher (`Dispatch`); Job/JobStore (`Jobs`); LogBuffer/LogLevel (`Logging`) | Newtonsoft |
| `Shtl.Mcp.Registry` | `~/.unity-mcp/registry.json`, heartbeat, выбор порта, дедуп serverName | InstanceEntry/RegistryStore (`Registry`); Fnv/PortAllocator/ServerName (`Common`) | Newtonsoft |
| `Shtl.Mcp.Tools` | Реестр инструментов, реализации Core-команд, no-throttle | все тулы + ITool/IEditorContext/ToolRegistry/TestRunnerNoThrottle (`Tools`) | Dispatcher, Newtonsoft, *.TestRunner |
| `Shtl.Mcp.Lifecycle` | `InitializeOnLoad`, `AssemblyReloadEvents`, watchdog, авто-старт, композиция | EditorContext/ShtlMcpServer/Bootstrap/Config (`Lifecycle`); DispatchingToolInvoker (`Server`) | Registry, Dispatcher, Transport, Tools, Newtonsoft |
| `Shtl.Mcp.UI` | Дашборд (UI Toolkit `EditorWindow`) | DashboardWindow (`UI`) | Lifecycle |

DAG: Transport/Dispatcher/Registry — листья; Tools→Dispatcher; Lifecycle→{Registry,Dispatcher,Transport,
Tools}; UI→Lifecycle. **Разрыв цикла Lifecycle↔Tools** (Unity запрещает циклы): `TestRunCallbacks`
получает `JobStore` через DI, а не `ShtlMcpServer.Instance` (убрано ребро Tools→Lifecycle); `Logging`
размещён в Dispatcher (а не Lifecycle), чтобы GetLogsTool читал LogBuffer без захода в Lifecycle.

## Стратегия тестирования
- EditMode-тесты: парсер/роутер JSON-RPC, выбор порта и дедуп `serverName`,
  сериализация записи реестра, реестр heartbeat/протухание, JobStore.
- PlayMode/интеграционный тест: соединение переживает play→edit-переход и
  принудительный `recompile` (RED-gate: подтверждаемо падает без re-spawn логики).
- Поведение, а не приватное состояние: тесты на публичный контракт инструментов.

## Связанные страницы
- `wiki/systems/multi-instance.md` — маршрутизация и discovery.
- `wiki/systems/lifecycle-and-reload.md` — выживание при domain reload, async-job.
- `wiki/systems/command-set.md` — полный набор инструментов.
- `wiki/systems/dashboard.md` — UI.
