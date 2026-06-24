---
entity: command-set
content_class: intent-derived
source_refs:
  - raw/features/F3-command-set.md
compiled_at_commit: pending
epoch: 001
status: active
needs_review: false
---

# Набор команд (инструменты MCP)

Философия: **тонкое ядро + escape hatches**. Маленькая поверхность, полная мощь
через `execute_menu_item` (вся встроенная функциональность Unity бесплатно) и
`run_csharp` (произвольный Editor-C# для длинного хвоста). Набор выведен из prior
art (CoderGamester/mcp-unity, IvanMurzak/Unity-MCP, CoplayDev/unity-mcp) и сжат до
лёгкого ядра.

Легенда: ⏳ = async-job (возвращает `jobId`, опрос через `get_job`).
Все инструменты работают в Edit и Play (см. `lifecycle-and-reload.md`, AC4.5).

## Core v1 (~20 инструментов)

### Контроль и идентичность
| Инструмент | Назначение |
|---|---|
| `status` | Идентичность инстанса (projectName/path/version/port/pid), mode, isCompiling, health, uptimeSeconds. Якорь «какой инстанс + жив ли». Наблюдаемость re-spawn (F4/AC4.7): `reloadCount` (пережитых domain reload за сессию, durable) + `listenerUptimeSeconds` (время текущего listener'а, сбрасывается при re-spawn). |
| `set_play_mode(playing)` ⏳ | Войти/выйти из Play. |
| `recompile` ⏳ | Принудительная перекомпиляция скриптов. |
| `get_job(jobId)` | Опрос статуса/результата async-job. |

### Консоль
| `get_logs(filter, count)` | Чтение консоли (errors/warnings/logs) — главный цикл обратной связи. |
| `clear_logs` | Очистить консоль. |

### Ассеты
| `refresh_assets` ⏳ | `AssetDatabase.Refresh`. |
| `find_assets(filter)` | Поиск/листинг ассетов. |
| `read_asset(path)` | Сериализованные данные ассета. |
| `move_asset` / `delete_asset` / `create_folder` | CRUD по AssetDatabase. |

### Префабы
| `create_prefab(fromGameObject, path)` | GameObject → префаб-ассет. |
| `open_prefab(path)` / `save_prefab` / `close_prefab` | Редактирование в prefab-stage. |
| `instantiate_prefab(path, parent?)` | Инстанс префаба в сцену. |

### Сцена / иерархия / объекты
| `get_hierarchy(scene?)` | Дерево объектов. |
| `find_gameobject(query)` | Поиск GameObject. |
| `gameobject_create / gameobject_modify / gameobject_destroy` | CRUD GameObject. |
| `set_parent(child, parent)` | Репарентинг. |
| `get_object(ref)` / `modify_object(ref, changes)` | Обобщённое чтение/запись через `SerializedObject` — компактно покрывает компоненты и поля (вместо россыпи component_*). |
| `open_scene(path)` / `save_scene` | Сцены. |
| `get_selection` / `set_selection` | Выделение в Editor. |

### Тесты
| `run_tests(mode, filter)` ⏳ | EditMode/PlayMode тесты → результаты. |

### Escape hatches
| `execute_menu_item(path)` | Исполнить любой `MenuItem`. |
| `run_csharp(code)` | Скомпилировать и выполнить произвольный Editor-C#; вернуть результат/ошибки. Включаемо конфигом (footgun). |

### Зрение
| `screenshot(view = game \| scene)` | Кадр Game/Scene View как изображение — модель «видит» результат. |

## Контракт инструмента
- JSON-схема параметров + человекочитаемое описание в `tools/list` (AC3.4).
- Ответ всегда включает идентичность инстанса (`projectName`) (INV-3).
- Ответ несёт `recoveryHint` со ссылкой на реестр (F7/AC7.3); `status` и
  `initialize.instructions` содержат пре-брифинг восстановления (F7/AC7.2,
  см. `recovery-discoverability.md`).
- Ошибки — структурированные (понятный текст + код), не исключение наружу.

## v2 / опционально (достижимо через escape hatches)
Профайлер, packages CRUD, материалы/шейдеры-специфика, reflection-API,
isolated/camera-screenshot, `resources/*` MCP, SSE-стрим прогресса/логов.
