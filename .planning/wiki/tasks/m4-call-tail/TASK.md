# TASK — m4-call-tail (M4/T1)

## Цель
Закрыть **F5/AC5.5**: дашборд показывает хвост последних N MCP-вызовов (время, метод, статус ✓/✗,
длительность). Сейчас есть только `LastRequestAgeSeconds` (одна метка) — истории нет.

## Привязка
- Фича/AC: **F5 / AC5.5** (дашборд). Реализация уже зафиксированного raw — forward-поток не нужен.
- Системы: `dashboard.md` (макет), `architecture.md` (asmdef DAG).
- Инварианты: INV-4 (единственное окно, ничего лишнего — call-tail сам по AC5.5, допустим).

## Подход
- `CallTail` (ring-buffer, **в Lifecycle** — его видит UI; Transport получает только делегат `Action`,
  без type-зависимости, DAG цел). Транзиентно, in-memory; потокобезопасно (запись с фон-HTTP-потока,
  чтение с главного → lock).
- `McpRouter.Handle` (ветка `tools/call`) инструментируется: имя тула, ok (нет исключения и нет `error`
  в результате), длительность (`Stopwatch`). Запись через инжектируемый `recordCall`.
- `ShtlMcpServer` владеет `CallTail`, инжектит `Record` в роутер, отдаёт `RecentCalls()` дашборду.
- `DashboardWindow` рендерит хвост (foldout, обновление в существующем `Refresh()`/`Update()`).

## Acceptance
- После серии MCP-вызовов дашборд показывает их хвост (новейшие сверху): метод, ✓/✗, мс, «N с назад».
- Буфер ограничен N (старые вытесняются).
- `ok=false` для тула, вернувшего `{error}` или бросившего исключение.
- DAG сборок цел (Transport не получает зависимости на Lifecycle/Dispatcher).
- Регресс: полный EditMode-сьют зелёный; + unit-тесты CallTail + recording-assert в McpRouterTests.

## Шаги
1. `CallTail.cs` (Lifecycle) + unit-тесты (cap/порядок/ok).
2. `McpRouter`: `recordCall`-параметр + инструментирование `tools/call` + assert в McpRouterTests.
3. `ShtlMcpServer`: владение CallTail, инжекция, `RecentCalls()`.
4. `DashboardWindow`: рендер хвоста.
5. refresh_assets → компиляция → сьют → e2e (серия вызовов → проверка записи).

## Статус
✅ Done — CallTail + router-инструментирование + дашборд-рендер. 121/121, DAG цел. Визуал — за человеком.
