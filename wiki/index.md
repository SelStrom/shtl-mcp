# Wiki — каталог

Сначала читай этот индекс, потом проваливайся в страницы. Обновляется при каждом
изменении wiki.

## systems/ — архитектура (intent-derived, gate между намерением и кодом)
- [architecture](systems/architecture.md) — общая схема: in-Unity HTTP MCP,
  модель потоков, стек, разбиение на asmdef, стратегия тестов.
- [multi-instance](systems/multi-instance.md) — порт по пути, реестр, дедуп
  serverName, `claude mcp add`, префикс инструментов, клоны/worktrees, scope.
- [lifecycle-and-reload](systems/lifecycle-and-reload.md) — выживание при domain
  reload, async-job, watchdog, главный поток, Reload-Domain рекомендация.
- [command-set](systems/command-set.md) — полный набор инструментов (тонкое ядро
  + escape hatches), async-команды, контракт, v2.
- [recovery-discoverability](systems/recovery-discoverability.md) — как
  использующая модель узнаёт о восстановлении (durable-реестр, пре-брифинг,
  recoveryHint, opt-in host-крошка).
- [dashboard](systems/dashboard.md) — UI Toolkit окно, макет, элементы.

## concepts/ — (пока пусто; intent-derived понятия по мере надобности)

## code/ — (пока пусто; reference-страницы по коду появятся при реализации, с pin к коммиту)

## explorations/ — (пока пусто; синтез из query)

## tasks/ — история тасков
- [README](tasks/README.md) — конвенция ведения истории тасков.
- [m1-walking-skeleton](tasks/m1-walking-skeleton/TASK.md) — вертикальный срез:
  connect + status/get_logs + выживание при reload. План — `PLAN.md` (13 задач, TDD).

## Карта намерения (raw/)
- `raw/domain/overview.md` — сущности, инварианты INV-1..5, ограничения.
- `raw/features/F1..F6` — фичи с acceptance criteria.
- `raw/epochs.md` — эпохи.
