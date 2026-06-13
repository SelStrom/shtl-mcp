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

## Out of scope (v2+, достижимо через escape hatches)
- Профайлер, packages CRUD, материалы/шейдеры-специфика, reflection-API,
  isolated-screenshot.
