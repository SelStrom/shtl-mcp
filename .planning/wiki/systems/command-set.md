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

## Core (44 инструмента: v1 + M5 command-set v2)

M5 (command-set v2, murzak-parity) добавил 9 инструментов и расширил 2 по итогам
аудита замены murzak в PerfectWar: частые операции, для которых escape hatch
объективно неудобен (`run_csharp` выключен по умолчанию). F3/AC3.9–3.14.

### Контроль и идентичность
| Инструмент | Назначение |
|---|---|
| `status` | Идентичность инстанса (projectName/path/version/port/pid), mode, isCompiling, health, uptimeSeconds. Якорь «какой инстанс + жив ли». Наблюдаемость re-spawn (F4/AC4.7): `reloadCount` (пережитых domain reload за сессию, durable) + `listenerUptimeSeconds` (время текущего listener'а, сбрасывается при re-spawn). |
| `ping` | Bg-liveness (F4/AC4.8): отвечает с фонового потока, даже когда главный поток заблокирован (модал/компиляция) — отличает подвисший главный поток от мёртвого сервера. |
| `get_config` | Снимок конфига (port range, heartbeat, footgun-флаг, keepalive) — диагностика (F2). |
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
| `write_asset(path, content, refresh?, createFolders?)` ⏳ | Создание/перезапись текстового ассета под `Assets/` — парный к `read_asset` (AC3.9). Для компилируемых (`.cs`/`.asmdef`/`.asmref`) при `refresh` — async-job (jobId; ошибки компиляции через `get_job`), остальные — синхронно. Обычный тул, не footgun. |
| `move_asset` / `delete_asset` / `create_folder` | CRUD по AssetDatabase. |
| `create_asset(path, type, shader?, overwrite?)` | Бинарный ассет через `AssetDatabase.CreateAsset` (AC3.15): материал (нужен `shader`), наследник `ScriptableObject`, прочие `UnityEngine.Object` с ctor без параметров. Текстовые файлы — через `write_asset`; поля созданного ассета — через `modify_object`. Существующий путь → ошибка без `overwrite`. Вне scope: типы с аргументами конструктора (`Texture2D`, `RenderTexture`). |

### Префабы
| `create_prefab(fromGameObject, path)` | GameObject → префаб-ассет. |
| `open_prefab(path)` / `save_prefab` / `close_prefab` | Редактирование в prefab-stage. Пока стейдж открыт, он — **контекст** для объектных тулов (AC3.16). |
| `instantiate_prefab(path, parent?)` | Инстанс префаба в текущий контекст (стейдж, иначе активная сцена). `parent` — объект того же контекста; локальный трансформ берётся авторский из префаба (мировой сломал бы якоря `RectTransform`). Возвращает `path` инстанса и `context`. |

### Сцена / иерархия / объекты
> **Контекст работы (AC3.16).** Все тулы этой секции адресуют объекты **текущего контекста**: открытый
> prefab-stage, иначе активная сцена. Единственная точка знания — `SceneObjects.Roots()`, поэтому резолв
> по пути/имени, обход иерархии и создание объектов переключаются вместе. Так же ведёт себя Unity: при
> открытом стейдже Hierarchy показывает префаб, а объекты сцены недостижимы. `get_hierarchy`,
> `find_gameobject` и `instantiate_prefab` возвращают `context` (`scene` | `prefabStage` + путь ассета) —
> без него пустой результат читается как «объекта нет» вместо «искал не там».

| `get_hierarchy(scene?)` | Дерево объектов текущего контекста. |
| `find_gameobject(query)` | Поиск GameObject в текущем контексте. |
| `gameobject_create / gameobject_modify / gameobject_destroy` | CRUD GameObject. |
| `set_parent(child, parent)` | Репарентинг. |
| `add_component(target, type)` / `remove_component(target, type, index?)` | Жизненный цикл компонента (AC3.10) — дополняет `modify_object` (тот пишет свойства, но не создаёт/не удаляет компонент). Тип не найден → ошибка со списком-подсказкой; `DisallowMultipleComponent`/`RequireComponent` — внятная ошибка. |
| `get_object(ref)` / `modify_object(ref, changes)` | Обобщённое чтение/запись через `SerializedObject` — компактно покрывает компоненты и поля (вместо россыпи component_*). M5 (AC3.11): bulk-массив изменений + вложенные пути (`m_Size.x`) в одной транзакции; target — scene-GO, asset-path или instanceId (ScriptableObject/материал/конфиг); настраиваемая глубина чтения. AC3.11г: пишутся ссылочные поля (`ObjectReference`) — значение той же формы, что и target, `null` снимает ссылку; поле ждёт компонент, а указан GameObject → берётся компонент с него; неподходящий тип → ошибка, транзакция откатывается. Вне scope: встроенные ресурсы Unity и ссылка на объект сцены в поле ассета (Unity её не сериализует). |
| `open_scene(path)` / `save_scene` | Сцены. |
| `list_scenes` / `create_scene` / `unload_scene` / `set_active_scene` | Multi-scene (AC3.13): открытые сцены (path/isLoaded/isActive/isDirty), создание (опц. сохранение в asset), выгрузка, активная сцена. Аддитивные сценарии поверх `open_scene`/`save_scene`. |
| `get_selection` / `set_selection` | Выделение в Editor. `set_selection` принимает и путь/имя объекта текущего контекста, и asset-path (та же форма target, что у `get_object`/`modify_object`); объект контекста приоритетнее. |

### Тесты
| `run_tests(mode, filter)` ⏳ | EditMode/PlayMode тесты → результаты. |

### Reflection (AC3.12)
| `call_method(type, method, parameterTypes?, args?, target?, assembly?)` | Вызов существующего C#-метода (static/instance, вкл. private) по типу и сигнатуре. Обычный тул, не footgun: вызов существующего метода безопаснее компиляции произвольного кода (`run_csharp`). |
| `find_method(type, nameContains?)` | Найти сигнатуры методов типа — выбор перегрузки перед `call_method`. |

### Escape hatches
| `execute_menu_item(path)` | Исполнить любой `MenuItem`. |
| `run_csharp(code)` | Скомпилировать и выполнить произвольный Editor-C#; вернуть результат/ошибки. Включаемо конфигом (footgun). |

### Зрение
| `screenshot(view = game \| scene, camera?)` | Кадр Game/Scene View как изображение — модель «видит» результат. `camera` (AC3.14) — кадр конкретной камеры по имени/пути GO, приоритетнее `view`. |

## Контракт инструмента
- JSON-схема параметров + человекочитаемое описание в `tools/list` (AC3.4).
- Ответ всегда включает идентичность инстанса (`projectName`) (INV-3).
- Ответ несёт `recoveryHint` со ссылкой на реестр (F7/AC7.3); `status` и
  `initialize.instructions` содержат пре-брифинг восстановления (F7/AC7.2,
  см. `recovery-discoverability.md`).
- Ошибки — структурированные (понятный текст + код), не исключение наружу.

## Кастомные инструменты хоста (F3/AC3.6–3.8)
Host-проект расширяет тулсет **без форка**: кладёт в свою Editor-сборку класс,
реализующий `ITool` и помеченный `[McpTool]` (оба из `Shtl.Mcp.Tools`; host-asmdef
референсит `Shtl.Mcp.Tools` + `Newtonsoft.Json`). `[McpTool]` — **маркер класса** (не
метод-атрибут): схема пишется вручную как `JObject` (как у встроенных), метод-авто-схема
намеренно НЕ делалась (несовместима со стеком .NET Standard/Mono/Newtonsoft; см.
[[architecture]] — SDK отвергнут). Дискавери — `TypeCache.GetTypesWithAttribute<McpTool>`
в `ShtlMcpServer.EnsureStarted` ПОСЛЕ встроенных (и на каждом reload; TypeCache дёшев).
- **Изоляция:** битый тул (исключение в ctor или в геттере `Name`/`Description`/`InputSchema`
  / нет parameterless-ctor / пустой `Name`) → warning в консоль + пропуск, старт не падает.
  Контракт прогревается на обнаружении → битый тул не отравит `tools/list`. Ctor исполняется
  на главном потоке при старте/reload → обязан быть дешёвым (тяжёлый застопорит старт).
- **Приоритет:** имя = уже зарегистрированному → пропуск позднего (встроенные первыми →
  приоритетны; кастом-vs-кастом: детерминированно побеждает лексикографически первый по FullName —
  `TypeCache`-порядок undefined, поэтому дискавери сортирует). Без тихого override.
- **Не footgun:** обычный тул, не требует `AllowRunCsharp`.
- **Зависимости:** MVP — parameterless-ctor + прямые UnityEditor/UnityEngine API в
  `Invoke`. DI/контекст (доступ к JobStore/async-job) и метод-атрибут+авто-схема — v2.
- Новый тул виден клиенту после reconnect (`notifications/tools/list_changed` не шлём —
  клиенты и так переподключаются). Файл вне Editor-сборки TypeCache не увидит.

## Опционально / длинный хвост (достижимо через escape hatches)
Профайлер, packages CRUD, материалы/шейдеры-специфика, isolated-screenshot,
`type-get-json-schema`, built-in-ресурсы, `resources/*` MCP, SSE-стрим
прогресса/логов. Частая нужда в одном из них у конкретного хоста → host custom
tool (`[McpTool]`, AC3.6), не расширение ядра.
