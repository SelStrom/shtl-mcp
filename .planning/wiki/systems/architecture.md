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
Каждая сборка — одна ответственность, тестируется отдельно:

| asmdef | Ответственность | Ключевые зависимости |
|---|---|---|
| `ShtlMcp.Transport` | HttpListener + парсер/сериализатор JSON-RPC, маршрутизация MCP-методов | Newtonsoft (без Unity, где можно → юнит-тестируемо вне Editor) |
| `ShtlMcp.Dispatcher` | Очередь команд, исполнение в главном потоке, JobStore (SessionState) | UnityEditor |
| `ShtlMcp.Registry` | Чтение/запись `~/.unity-mcp/registry.json`, heartbeat, выбор порта, дедуп serverName | UnityEditor |
| `ShtlMcp.Tools` | Реестр инструментов (`[McpTool]`), реализации Core-команд | UnityEditor |
| `ShtlMcp.Lifecycle` | `InitializeOnLoad`, `AssemblyReloadEvents`, watchdog, авто-старт | UnityEditor |
| `ShtlMcp.UI` | Дашборд (UI Toolkit `EditorWindow`) | UnityEditor + UIToolkit |

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
