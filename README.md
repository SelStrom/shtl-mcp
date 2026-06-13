# shtl-mcp

Лёгкий **самодостаточный** MCP-сервер, встроенный прямо в Unity Editor — без
внешнего процесса-моста. Даёт LLM-агентам (Claude Code и др.) полноценный
контроль над инстансом Unity: компиляция, ассеты, префабы, сцены, play/edit,
тесты — в Edit и Play mode.

## Ключевые свойства
- **Self-contained** — только Unity-пакет; ноль артефактов в папке LLM-клиента.
- **Multi-instance** — каждый инстанс Unity = свой HTTP-сервер; модель адресует
  инстанс по префиксу инструментов (`mcp__unity-<project>__*`).
- **Survives domain reload** — listener и async-job переживают перекомпиляцию и
  play/edit-переход (watchdog + `SessionState` + re-spawn).
- **Тонкое ядро + escape hatches** — компактный набор команд + `run_csharp` /
  `execute_menu_item` для длинного хвоста.
- **UI Toolkit дашборд** — одно минимальное информационное окно.

## Статус
Greenfield. Зафиксированы намерение и архитектура; код — следующий шаг.

## Навигация (intent-driven docs)
- [`CLAUDE.md`](CLAUDE.md) — схема агента: флоу работы `raw → wiki → code`.
- [`raw/`](raw/) — намерение (источник истины): домен, инварианты, фичи F1–F6.
- [`wiki/`](wiki/) — архитектура и компилируемый контекст; начинать с
  [`wiki/index.md`](wiki/index.md).

Целевая платформа: Unity 2022 LTS+. Лицензия/контрибьютинг — TBD.
