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
