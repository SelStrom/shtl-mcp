# TASK: M1 — Walking Skeleton

**Status:** planned
**Привязка:** raw F1 (multi-instance), F2 (restart/watchdog), F3 (часть: status/get_logs),
F4 (play/edit + reload), F5 (минимальный дашборд), F6 (self-contained).
Системы: `wiki/systems/{architecture, multi-instance, lifecycle-and-reload, command-set, dashboard}.md`.

## Цель
Из Claude Code подключиться к встроенному в Unity MCP-серверу одной командой
`claude mcp add`, вызвать `status` и `get_logs`, и доказать, что сервер переживает
перекомпиляцию и play/edit-переход (watchdog + re-spawn + SessionState). Это
вертикальный срез, снимающий все ключевые риски проекта.

## Acceptance (срез M1)
- AC1.1/1.4/1.5/1.6 (порт по пути, реестр, `claude mcp add`, `status`).
- AC2.2/2.4 (watchdog-выживание, кнопка Restart).
- AC3.4/AC3.1-частично (`status`, `get_logs` с JSON-схемой в `tools/list`).
- AC4.1/4.6 (re-spawn вокруг reload, Unity API только в гл. потоке).
- AC5.1/5.2/5.3/5.6 (одно окно: статус, строка подключения+copy, Restart).
- AC6.1/6.2/6.5 (только Unity-пакет, без внешних процессов, только Newtonsoft).

## Вне M1 (→ M2/M3)
async-job, `run_csharp`/`execute_menu_item`, префабы/ассеты/сцена, `screenshot`,
control-flag, F7 discoverability, доводка дашборда.

## План
См. `PLAN.md` (bite-sized, TDD где применимо). Журнал исполнения — `journal.md`.
