# shtl-mcp

Лёгкий **самодостаточный** MCP-сервер, встроенный прямо в Unity Editor — без
внешнего процесса-моста. Даёт LLM-агентам (Claude Code и др.) контроль над
инстансом Unity по HTTP/JSON-RPC.

> Статус: **M1 — Walking Skeleton** (`0.1.0`). Рабочий вертикальный срез: транспорт,
> выживание при reload, мульти-инстанс, инструменты `status` / `get_logs`, дашборд.

## Этот репозиторий

Репо — **dev-проект Unity, в котором разрабатывается пакет**, плюс intent-driven
документация. Сам пакет — embedded UPM в `Packages/com.shtl.mcp/`.

```
Packages/com.shtl.mcp/   ← ПАКЕТ (package.json, Editor/, Tests/, README, LICENSE, CHANGELOG)
Assets/ ProjectSettings/ ← dev-проект Unity (для прогона EditMode-тестов)
raw/  wiki/  CLAUDE.md    ← intent-driven docs (НЕ импортируются потребителю пакета)
```

## Установка пакета (UPM)

Package Manager → **Add package from git URL…**:

```
https://github.com/SelStrom/shtl-mcp.git#upm
```

Ветка `upm` — чистый корневой пакет (публикуется `git subtree` из dev-моно-репо
`main`): импортируется только пакет, dev-проект и `raw/wiki` к потребителю не попадают.
Подробности использования — [README пакета](Packages/com.shtl.mcp/README.md).

## Ключевые свойства
- **Self-contained** — только Unity-пакет; ноль артефактов в папке LLM-клиента
  (единственное касание — `claude mcp add`).
- **Multi-instance** — каждый инстанс Unity = свой HTTP-сервер; адресация по
  префиксу инструментов `mcp__unity-<project>__*`; реестр `~/.unity-mcp/registry.json`.
- **Survives domain reload** — listener переживает перекомпиляцию и play/edit-переход
  (watchdog + `SessionState` + re-spawn).
- **UI Toolkit дашборд** — одно минимальное информационное окно (`Window/Shtl MCP`).

## Разработка
Открыть репозиторий как проект в Unity 2022.3+. Прогон тестов — см.
[`wiki/tasks/m1-walking-skeleton/PLAN.md`](wiki/tasks/m1-walking-skeleton/PLAN.md)
(«Соглашения по запуску тестов»). Текущая карта кода —
[`wiki/code/m1-server.md`](wiki/code/m1-server.md).

## Документация (intent-driven)
- [`CLAUDE.md`](CLAUDE.md) — схема агента: флоу `raw → wiki → code`.
- [`raw/`](raw/) — намерение (источник истины): домен, инварианты, фичи F1–F7.
- [`wiki/`](wiki/) — архитектура и компилируемый контекст; начинать с
  [`wiki/index.md`](wiki/index.md).

## Релиз пакета (для мейнтейнеров)
Ветка `upm` — это содержимое `Packages/com.shtl.mcp/`, нарезанное `git subtree`
(канонический приём Unity: `package.json` в корне публикуемой ветки, install без
`?path`). Публикация/обновление:

```
git branch -D upm 2>/dev/null
git subtree split --prefix=Packages/com.shtl.mcp -b upm
git push -f origin upm
```

## Лицензия
[MIT](Packages/com.shtl.mcp/LICENSE.md). Целевая платформа: Unity 2022 LTS+.
