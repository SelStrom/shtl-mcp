---
content_class: intent-derived
epoch: 001
status: active
---

# F3 — Набор команд

**Требование #3.** Базовый набор, позволяющий моделям полноценно работать с
Unity: перекомпиляция, префабы, play/edit, ассеты, тесты. Точный набор выведен
исследованием prior art (3 ведущих Unity-MCP) — см. `wiki/systems/command-set.md`.

Философия (зафиксирована): **тонкое ядро + escape hatches**. Маленькая поверхность
инструментов, полная мощь — через `execute_menu_item` (вся встроенная
функциональность Unity) и `run_csharp` (произвольный Editor-C# для длинного хвоста).

## Acceptance criteria

- **AC3.1** — Реализован Core-набор (полный перечень — `wiki/systems/command-set.md`):
  `status`, `set_play_mode`, `recompile`, `get_job`, `get_logs`, `clear_logs`,
  `refresh_assets`, `find_assets`, `read_asset`, `move_asset`, `delete_asset`,
  `create_folder`, `create_prefab`, `open_prefab`/`save_prefab`/`close_prefab`,
  `instantiate_prefab`, `get_hierarchy`, `find_gameobject`,
  `gameobject_create`/`gameobject_modify`/`gameobject_destroy`, `set_parent`,
  `get_object`/`modify_object` (через SerializedObject), `open_scene`/`save_scene`,
  `get_selection`/`set_selection`, `run_tests`, `execute_menu_item`, `run_csharp`,
  `screenshot`.
- **AC3.2** — `run_csharp` исполняет произвольный Editor-C# и возвращает
  результат/ошибки компиляции; включаемо/выключаемо конфигом (footgun-флаг).
- **AC3.3** — `execute_menu_item` исполняет любой `MenuItem` по строковому пути.
- **AC3.4** — Каждый инструмент имеет JSON-схему параметров и человекочитаемое
  описание, отдаётся в `tools/list`.
- **AC3.5** — Долгие/reload-команды (`set_play_mode`, `recompile`, `run_tests`,
  тяжёлый `refresh_assets`) возвращают `jobId`; результат — через `get_job`.
- **AC3.6** — **Кастомные инструменты хоста без форка.** Host-проект добавляет свой
  MCP-инструмент, положив в СВОЮ Editor-сборку класс, реализующий `ITool` и помеченный
  `[McpTool]` (оба из `Shtl.Mcp.Tools`). Без правок кода shtl-mcp и без ручной
  регистрации: сервер находит такие классы рефлексией (Unity `TypeCache`) при старте и
  после каждого domain reload и регистрирует наравне со встроенными. Контракт — тот же,
  что у встроенных (AC3.4): `Name`/`Description`/`InputSchema` (Newtonsoft `JObject`,
  пишется вручную)/`NeedsMainThread` (true → главный поток через dispatcher, false →
  фоновый HTTP-поток)/`Invoke`. Идентичность инстанса и `recoveryHint` навешивает
  транспорт (INV-3, F7/AC7.3).
- **AC3.7** — **Изоляция и приоритет при обнаружении.** Кастомный инструмент,
  бросивший исключение в конструкторе, без public parameterless-ctor или с пустым
  `Name`, пропускается с предупреждением в консоль и НЕ роняет старт сервера; остальные
  регистрируются. Имя, совпадающее с уже зарегистрированным (встроенным или ранее
  найденным кастомным), — детерминированный пропуск позднего, без тихого override
  (встроенные приоритетны, т.к. регистрируются первыми).
- **AC3.8** — **Кастомные ≠ footgun.** Кастомные инструменты — обычные
  зарегистрированные инструменты; механизм `[McpTool]` НЕ требует и не включает
  footgun-флаг `AllowRunCsharp` (в отличие от `run_csharp`).

## Out of scope (v2+, достижимо через escape hatches)
- Профайлер, packages CRUD, материалы/шейдеры-специфика, reflection-API,
  isolated-screenshot.
