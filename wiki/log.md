# Журнал операций (append-only)

Хронологический лог: forward-изменения, query, lint, drift-расследования, эпохи.
Префикс парсибелен: `grep "^## \[" log.md | tail -10`.

## [2026-06-13] epoch | 001 Bootstrap
Старт проекта. Зафиксированы намерение (raw/) и архитектура (wiki/systems/).

## [2026-06-13] forward | bootstrap spec | brainstorming
Создано из брейншторма: raw/domain/overview, raw/features/F1-F6, raw/epochs;
wiki/systems/{architecture, multi-instance, lifecycle-and-reload, command-set,
dashboard}; CLAUDE.md (схема агента). Код ещё не написан (greenfield) — следующий
шаг: writing-plans → план реализации.
Решения брейншторма: in-Unity Streamable HTTP MCP (без внешнего процесса);
мульти-инстанс через реестр + детерм. порт + префикс инструментов; watchdog +
reconnect; тонкое ядро + run_csharp/execute_menu_item; UI Toolkit дашборд;
Claude Code (итер.1), Unity 2022+; регистрация user-level.

## [2026-06-13] forward | F2 control-flag recovery | discuss
Добавлен LLM-инициируемый форс-рестарт: control-flag канал
`~/.unity-mcp/<serverName>.cmd`, исполняемый watchdog'ом независимо от listener'а
(работает, когда сервер недоступен; без демона; Unity не запускаем). Правки:
raw F2 (AC2.6/AC2.7), raw/domain (Control channel + INV-5), raw F6 (AC6.4),
wiki lifecycle (§4 Control-channel), wiki multi-instance (флаги в ~/.unity-mcp/),
CLAUDE.md (§Recovery playbook).

## [2026-06-13] forward | F7 recovery discoverability | discuss
Закрыт вопрос «как использующая модель узнает о восстановлении» (она не видит
dev-репо; при мёртвом сервере MCP-канал недоступен). Defense-in-depth по 3
каналам + opt-in host-крошка. Решение cold-start: вариант A (строго
самодостаточно по умолчанию) + предложение добавить указатель в host-CLAUDE.md
или recovery-скилл только с явного согласия. Правки: новый raw F7; raw/domain
INV-2 (исключение opt-in); raw F6 (AC6.1); raw F2 (AC2.7 cross-ref); новая wiki
systems/recovery-discoverability; wiki index; wiki command-set (recoveryHint +
пре-брифинг в контракте); CLAUDE.md §Recovery (две аудитории).

## [2026-06-13] plan | M1 walking skeleton | wiki/tasks/m1-walking-skeleton
Написан bite-sized план реализации (13 задач, TDD для чистой логики, integration/
manual для Unity-склейки). Декомпозиция проекта на вехи: M1 (этот срез), M2 (полный
набор инструментов + async-job + control-flag), M3 (F7 discoverability + доводка UI).
Локация планов — wiki/tasks/<slug>/PLAN.md (override дефолта skill'а под intent-driven
фреймворк). Отклонение: M1 — один Editor-asmdef с папками (6-asmdef split → M2).
