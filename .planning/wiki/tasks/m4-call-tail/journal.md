# Journal — m4-call-tail (M4/T1, append-only)

## [2026-07-01] реализация (AC5.5)

Хвост последних N MCP-вызовов в дашборде.

- **`CallTail`** (Lifecycle) — ring-buffer на `LinkedList<Entry>` (метод, ok, мс, ticks), ёмкость 20,
  снимок новейшие-первыми, доступ под lock (пишется с фон-HTTP-потока, читается с главного). Размещён в
  Lifecycle (а не Dispatcher): его видит UI (UI→Lifecycle), а Transport получает только делегат
  `Action<string,bool,double>` — без type-зависимости, **DAG сборок цел** (Transport по-прежнему референсит
  только Newtonsoft).
- **`McpRouter`** — новый опц. параметр `recordCall`; ветка `tools/call` обёрнута `Stopwatch`, пишет
  `(name, ok, ms)`. `ok = нет исключения И result["error"] == null` (логическая ошибка тула → ✗). Только
  `tools/call` (не initialize/tools/list — это инфраструктура подключения).
- **`ShtlMcpServer`** — владеет `CallTail(20)`, инжектит `_calls.Record` в роутер, отдаёт
  `RecentCalls()` дашборду.
- **`DashboardWindow`** — foldout «Recent calls (N)», перерисовка в `Refresh()` (раз в секунду): новейшие
  сверху, `✓/✗ method  Nms  Ns ago`, ✗ подсвечены. UI-glue — тестов нет, визуальный осмотр за человеком.

**Тесты:** `CallTailTests` (5: empty/новейшие-первыми/вытеснение по ёмкости/ok+ms/null-метод) +
`McpRouterTests` (4: запись ok на успехе, ✗ на `{error}`, ✗ на исключении, не-tools/call не пишется).
Добавлен `err`-тул в FakeInvoker для проверки логической ошибки без исключения. **121/121 EditMode зелёные**,
компиляция чистая. Реализация F5/AC5.5 — raw не менялся.

**T1 done.**
