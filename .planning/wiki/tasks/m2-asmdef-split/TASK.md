# TASK: T2 — 6-asmdef split

**Status:** done (6 сборок собираются, цикл разорван, 52/52 + reload-spanning зелёные)
**Привязка:** `architecture.md` §Модульность (6 asmdef). Характеризационный рефакторинг — поведение не
меняется, RED-gate = текущие 52 теста + headless smoke остаются зелёными.

## Цель

Разбить единый `Shtl.Mcp.Editor` на 6 сборок по ответственности (изоляция + независимая тестируемость),
пока кодовая база мала и тулы не расплодились.

## Реализованная раскладка (папка → сборка → namespace'ы)

namespace'ы НЕ менялись (папка ≠ namespace в C#) → usings потребителей не трогались.

| Папка | asmdef | Содержимое (namespace) | references |
|---|---|---|---|
| `Editor/Transport/` | `Shtl.Mcp.Transport` | JsonRpc+ServerInfo, McpRouter, IToolInvoker (`Transport`); HttpServer (`Server`) | Newtonsoft |
| `Editor/Dispatcher/` | `Shtl.Mcp.Dispatcher` | MainThreadDispatcher (`Dispatch`); Job, JobStore (`Jobs`); LogBuffer, LogLevel/LogItem (`Logging`) | Newtonsoft |
| `Editor/Registry/` | `Shtl.Mcp.Registry` | InstanceEntry, RegistryStore (`Registry`); Fnv, PortAllocator, ServerName (`Common`) | Newtonsoft |
| `Editor/Tools/` | `Shtl.Mcp.Tools` | все тулы + ITool/IEditorContext/ToolRegistry + TestRunnerNoThrottle (`Tools`); AssemblyInfo (InternalsVisibleTo) | Dispatcher, Newtonsoft, *.TestRunner |
| `Editor/Lifecycle/` | `Shtl.Mcp.Lifecycle` | EditorContext, ShtlMcpServer, ShtlMcpBootstrap, ShtlMcpConfig (`Lifecycle`); DispatchingToolInvoker (`Server`) | Registry, Dispatcher, Transport, Tools, Newtonsoft |
| `Editor/UI/` | `Shtl.Mcp.UI` | DashboardWindow (`UI`) | Lifecycle |

DAG без циклов: DP/RG/TR — листья; TL→DP; LC→{RG,DP,TR,TL}; UI→LC.

## Разрыв цикла Lifecycle ↔ Tools (был запрещён Unity)

Граф зависимостей выявил один цикл, держался на двух рёбрах TL→LC:
- **`TestRunCallbacks` → `ShtlMcpServer.Instance.Jobs`** (callback пишет результат job). **Фикс:** DI —
  `TestRunCallbacks(JobStore jobs)` через конструктор; `RunTestsTool` передаёт `_jobs`,
  `ReattachIfPending(JobStore)` принимает store из `EnsureStarted`. Убрано `using Shtl.Mcp.Lifecycle`.
  Мёртвый после этого `ShtlMcpServer.Jobs` удалён.
- **`GetLogsTool` → `LogBuffer`** (Logging изначально планировался в Lifecycle). **Фикс:** Logging
  размещён в **Dispatcher** (инфра-лист графа) — GetLogsTool→LogBuffer и ShtlMcpServer→LogBuffer оба
  стали `→DP` (уже существующие рёбра). Чисто размещение, без правок кода.

## Отклонения от architecture.md (зафиксированы в самой architecture.md)

- Имена сборок — `Shtl.Mcp.*` (под namespace-префикс), а не `ShtlMcp.*` из черновой таблицы.
- `Logging/` → Dispatcher, `Common/` → Registry (architecture.md эти папки по сборкам не расписывал).
- `DispatchingToolInvoker` (namespace `Server`) → Lifecycle (composition glue ShtlMcpServer).

## Acceptance

- AC-1: 6 сборок компилируются раздельно. ✅ — `Library/ScriptAssemblies/Shtl.Mcp.{Transport,Dispatcher,
  Registry,Tools,Lifecycle,UI}.dll` присутствуют, старая `Shtl.Mcp.Editor.dll` исчезла, 0 ошибок.
- AC-2: ноль циклических asmdef-ссылок. ✅ — цикл LC↔TL разорван (DI + Logging→DP).
- AC-3 (характеризация): 52 теста + reload-spanning остаются зелёными. ✅ — полный сьют 52/52 против
  реального split, reloadCount 23→24 (ReloadSurvivalTests), сервер отзывчив (max-lat 0.10с).
