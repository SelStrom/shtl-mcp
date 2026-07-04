# M5 — Промпт-план: паритет с murzak для догфудинга в PerfectWar

> **Что это.** Вход для forward-потока (`raw → wiki → code`), НЕ raw и НЕ wiki. Здесь
> зафиксирован набор недостающих инструментов, выявленный при попытке заменить
> `com.ivanmurzak.unity.mcp` (murzak) на shtl-mcp в боевом проекте **PerfectWar**
> (Unity 6.3, ECS/Netcode/UI Toolkit). Задача агента-исполнителя в репо shtl-mcp:
> прогнать выбранные пункты через forward-поток (propose raw-diff → проверка
> консистентности с F3 → wiki-diff `systems/command-set.md` → ревью человеком → code +
> EditMode-тесты). Приоритеты и «оставить на escape hatches» — решение человека (INV
> «Human owns intent»); ниже — обоснованные рекомендации, не директивы.

## 1. Контекст и цель

shtl-mcp `0.4.0` (M4, feature-complete, 35 built-in тулов) функционально закрывает
**критический путь** PerfectWar: правка `.cs` → `recompile` → `run_tests` → `get_logs`
→ `set_play_mode` → `screenshot`. Догфудинг возможен уже сейчас. Но при аудите замены
всплыли gaps относительно murzak, которые дают трение в реальных сценариях проекта.

**Философия сохраняется (F3): тонкое ядро + escape hatches.** Поэтому не добавляем
«всё как у murzak». Порядок закрытия gap:

1. **Escape hatch** — `execute_menu_item` / `run_csharp` / host custom tool (`[McpTool]`).
2. **Расширение** существующего обобщённого тула (`get_object`/`modify_object`/`screenshot`).
3. **Новый first-class тул** — только если операция частая И escape hatch объективно
   неудобен/невозможен без footgun.

⚠️ Ключевое трение: `run_csharp` — **footgun, выключен по умолчанию** (`AllowRunCsharp=false`,
machine-local EditorPrefs). Значит «закрыть через run_csharp» на практике недоступно, пока
человек явно не включит флаг в host-проекте. Это усиливает аргумент за целевые тулы для
частых операций (reflection, add-component) — либо за осознанное включение `AllowRunCsharp`
в PerfectWar.

## 2. Матрица gap → решение → приоритет

| murzak-возможность | shtl `0.4.0` | Рекомендация | Приоритет |
|---|---|---|---|
| `script-update-or-create` — запись/создание `.cs` и текстовых ассетов | **нет записи текста** (только `read_asset`/`move`/`delete`) | **Новый тул** `write_asset` (парный к `read_asset`) | **P0** |
| add / remove **компонента** GameObject | `modify_object` пишет свойство, но не создаёт/не удаляет компонент | **Новые тулы** `add_component` / `remove_component` | **P0** |
| bulk / nested правка + правка **ассетов** (ScriptableObject/Inspector) | `modify_object` — одно свойство, только scene GO | **Расширить** `get_object`/`modify_object`: массив изменений, вложенные пути, target по asset-path/instanceId | **P0** |
| `reflection-method-find/call` — вызов произвольного C#-метода | только `run_csharp` (footgun, off) | **Новый тул** `call_method` (+ опц. `find_method`) | **P1** |
| multi-scene: list / create / unload / set-active | только `open_scene`/`save_scene` | **Расширить** набор: `list_scenes`, `create_scene`, `unload_scene`, `set_active_scene` | **P1** |
| screenshot конкретной камеры (по имени) | `screenshot` game/scene | **Расширить** `screenshot` параметром `camera` | **P1** |
| `gameobject-duplicate` | нет | **Новый small-тул** `gameobject_duplicate` (или `set_selection`+`execute_menu_item("Edit/Duplicate")`) | **P2** |
| `assets-copy` | `move`/`delete` есть | **Новый small-тул** `copy_asset` (симметрично `move_asset`) | **P2** |
| `assets-material-create` | нет | **Escape hatch**: `execute_menu_item("Assets/Create/Material")` + `modify_object`; либо host custom tool. F3 out-of-scope. | оставить |
| shaders list/get | `read_asset` читает `.shader` текст | **Escape hatch** / host tool. F3 out-of-scope. | оставить |
| `package-add/remove/list/search` | нет | **Escape hatch**: правка `manifest.json` + `refresh_assets`, либо `execute_menu_item` (Package Manager). F3 явно out-of-scope. | оставить |
| `type-get-json-schema` | нет | **Escape hatch** `run_csharp` / host tool. Редко. | оставить |
| `find-built-in` (Resources/unity_builtin_extra) | нет | **Escape hatch** `run_csharp`. Редко. | оставить |

## 3. Детальные спеки — P0

Контракт как у встроенных (F3/AC3.4): `Name` / `Description` / `InputSchema` (Newtonsoft
`JObject`, вручную) / `NeedsMainThread` / `Invoke`; структурированные ошибки (`{ "error": … }`),
не исключение наружу; идентичность инстанса и `recoveryHint` навешивает транспорт.

### 3.1 `write_asset` — запись текстового ассета / скрипта  **[P0]**
- **Назначение.** Создать или перезаписать текстовый ассет по пути (`.cs`, `.uxml`, `.uss`,
  `.json`, `.asmdef`, `.shader`, …). Парный к `read_asset`. Фундаментально: без него модель
  не может писать код через MCP — это ядро, не длинный хвост.
- **Параметры (InputSchema):** `path`* (string, под `Assets/`), `content`* (string),
  `refresh` (bool, default `true` — после записи `AssetDatabase.ImportAsset`/`Refresh`, чтобы
  `.cs` компилировался), `createFolders` (bool, default `true`).
- **NeedsMainThread:** `true` (AssetDatabase). **Async (⏳):** да, если `refresh` вызывает
  domain reload (как `refresh_assets`) — вернуть `jobId`, компиляционные ошибки/статус через
  `get_job`. Формат ошибок компиляции — как у murzak `script-update-or-create` (файл, строка,
  сообщение).
- **Поведение/ловушки:** запись только под `Assets/` (не `Packages/`); при существующем ассете
  — перезапись + reimport. При отказе записи (readonly/вне Assets) — структурированная ошибка.
- **murzak-аналог:** `script-update-or-create` (+ обобщение на любой текстовый ассет).
- **Открытый вопрос человеку:** footgun-статус. Запись `.cs` = исполнение произвольного кода
  после компиляции (косвенно). Но murzak даёт это без гейта, и модель всё равно пишет код
  через файловую систему. Рекомендация: **не гейтить** (обычный тул, как `delete_asset`),
  отделив от `run_csharp` (тот компилирует+исполняет ad-hoc в памяти).

### 3.2 `add_component` / `remove_component`  **[P0]**
- **Назначение.** Добавить/удалить компонент на GameObject. Их модель `modify_object` через
  `SerializedObject` пишет **свойства существующего** компонента, но не создаёт и не удаляет
  сам компонент — объективный gap, не покрываемый обобщённым тулом. Пара маленьких глаголов
  жизненного цикла, не «россыпь component_*».
- **`add_component` параметры:** `target`* (ref GO — как в `gameobject_modify`), `type`*
  (string, полное имя типа, напр. `UnityEngine.Rigidbody2D` или проектный
  `PerfectWar.Game.Ship.ShipView`). Возврат: ref добавленного компонента (для последующего
  `modify_object`).
- **`remove_component` параметры:** `target`*, `type`* (+ опц. `index` при нескольких
  одинаковых). 
- **NeedsMainThread:** `true`. **Async:** нет.
- **Ловушки:** тип не найден в загруженных сборках → структурированная ошибка со списком-подсказкой;
  дубликаты `[DisallowMultipleComponent]`; удаление required-компонента (`[RequireComponent]`)
  — вернуть внятную ошибку Unity, не глотать.
- **murzak-аналог:** `gameobject-component-add` / `gameobject-component-destroy`.

### 3.3 Расширение `get_object` / `modify_object`  **[P0]**
Их дизайн-ставка (F3): обобщённый `get_object`/`modify_object` через `SerializedObject`
«вместо россыпи component_*». Чтобы ставка держалась в бою — три расширения:
- **(a) Bulk / nested `modify_object`.** Сейчас — одно свойство. Принять **массив** изменений
  `changes: [{ component, property, value }, …]` и **вложенные пути** свойств
  (`property: "m_Size.x"`, массивы/вложенные структуры) в одной транзакции `SerializedObject`
  (`ApplyModifiedProperties` один раз). murzak-аналог: bulk `SerializedMember`.
- **(b) Target по ассету.** `get_object`/`modify_object` принимают не только scene-GO, но и
  **Unity Object по asset-path или instanceId** (ScriptableObject, материал, конфиг) — правка
  Inspector/SO из карты верификации PerfectWar (CLAUDE.md). murzak-аналог: `object-get-data` /
  `object-modify`.
- **(c) Глубина `get_object`.** Убедиться, что `get_object` отдаёт компоненты + сериализованные
  свойства достаточно глубоко для верификации состояния (GSD verifier использует
  `gameobject-component-get`).
- **NeedsMainThread:** `true`. Это **расширение**, не новые тулы — минимальный прирост
  поверхности, максимальный охват.

## 4. Детальные спеки — P1

### 4.1 `call_method` (+ опц. `find_method`)  **[P1]**
- **Назначение.** Вызвать существующий C#-метод (в т.ч. private, static/instance) по типу и
  сигнатуре. В PerfectWar реально нужен: bind UI Toolkit (`DataSourceBinder.BindLabels`),
  разрешение NFE client-world, программные входы (`MapFlowCoordinator`). Сейчас доступно только
  через `run_csharp`, который выключен → трение.
- **`call_method` параметры:** `type`* (string, полное имя), `method`* (string),
  `parameterTypes` (string[], для разрешения перегрузки), `args` (JSON[] → десериализуются в
  типы параметров), `target` (ref для instance-метода; пусто → static), `assembly` (опц.
  сужение поиска).
- **`find_method` (опц.) параметры:** `type`*, `nameContains` — вернуть найденные сигнатуры
  (как murzak `reflection-method-find`), чтобы модель выбрала перегрузку перед вызовом.
- **NeedsMainThread:** `true` (обычно трогает Unity API). **Async:** нет (если метод сам не
  триггерит reload — тогда как `recompile`).
- **Footgun?** Рекомендация: **не гейтить как run_csharp** — вызов *существующего* метода
  безопаснее компиляции произвольного кода. Но это решение человека; если гейтить — отдельным
  флагом, не `AllowRunCsharp`.
- **murzak-аналог:** `reflection-method-find` + `reflection-method-call`. F3 помечал
  reflection-API как v2 — этот пункт предлагает **поднять приоритет** из-за реальной боли.

### 4.2 Multi-scene: `list_scenes` / `create_scene` / `unload_scene` / `set_active_scene`  **[P1]**
- **Назначение.** PerfectWar многосценовый (Boot / DevBoot / GameWorld грузятся аддитивно;
  инциденты в памяти про scene-list и LoadMapStep). `open_scene`/`save_scene` недостаточно.
- **`list_scenes`:** вернуть открытые сцены (path, isLoaded, isActive, isDirty). murzak:
  `scene-list-opened`. NeedsMainThread `true`.
- **`create_scene`:** новая сцена (опц. сохранить в asset). murzak: `scene-create`.
- **`unload_scene`:** выгрузить открытую сцену (по path). murzak: `scene-unload`. Async если
  провоцирует тяжёлую операцию.
- **`set_active_scene`:** сделать открытую сцену активной. murzak: `scene-set-active`.
- Держать в едином домене scene-тулов; переиспользовать существующую политику dirty-сцен из
  `run_tests` (`scenePolicy`).

### 4.3 Расширение `screenshot` параметром `camera`  **[P1]**
- **Назначение.** Снять кадр конкретной камеры по имени GO (не только `game`=`Camera.main` /
  `scene`). Для визуальной верификации, когда важна не главная камера.
- **Параметры (добавить):** `camera` (string, имя GO с `Camera`); при заданном — рендер этой
  камеры в RenderTexture нужного размера. `view` остаётся (`game`/`scene`), `camera`
  взаимоисключим с `view` или имеет приоритет.
- **NeedsMainThread:** `true`. **murzak-аналог:** `screenshot-camera`. F3: camera-screenshot был
  v2 — расширение дешёвое, приоритет по потребности.

## 5. P2 (по желанию; escape hatch тоже приемлем)
- **`gameobject_duplicate(target)`** — дублировать GO с детьми. Альт: `set_selection` +
  `execute_menu_item("Edit/Duplicate")`. murzak: `gameobject-duplicate`.
- **`copy_asset(from, to)`** — копия ассета, симметрично `move_asset`. murzak: `assets-copy`.

## 6. Явно ОСТАВЛЕНО на escape hatches / host custom tools (обоснование)
Соответствует F3 «Out of scope (v2+)» и §v2 `command-set.md` — не тащить в ядро:
- **Материалы / шейдеры-специфика** — `execute_menu_item("Assets/Create/Material")` + `modify_object`,
  или host `[McpTool]`. Чтение — `read_asset`.
- **Packages CRUD** — редкая операция; правка `manifest.json` + `refresh_assets`, либо Package
  Manager через `execute_menu_item`.
- **`type-get-json-schema`, `find-built-in`, профайлер** — длинный хвост; `run_csharp`/host tool.

Если конкретный host (PerfectWar) упрётся в один из них часто — правильный путь по их же
архитектуре — **host custom tool** (`ITool` + `[McpTool]` в Editor-сборке PerfectWar,
референс `Shtl.Mcp.Tools` + `Newtonsoft.Json`), без форка shtl-mcp. Это отдельная маленькая
задача на стороне PerfectWar, не милстоун shtl-mcp.

## 7. Открытые вопросы человеку (решить до code-diff)
1. **Footgun-статус `write_asset` и `call_method`** — гейтить (`AllowRunCsharp` / отдельный флаг)
   или обычные тулы? Рекомендация: обычные (см. 3.1, 4.1).
2. **Именование:** `write_asset` (обобщённо, парно `read_asset`) vs узкий `write_script`.
   Рекомендация: `write_asset` + документировать `.cs`-семантику (компиляция при `refresh`).
3. **Объём M5** — минимальный (только P0) или P0+P1? P0 достаточно, чтобы догфудинг в PerfectWar
   перестал упираться в отсутствие записи кода и работы с компонентами.
4. **`AllowRunCsharp` в PerfectWar** — включить машинно-локально (тогда reflection/material
   через `run_csharp` доступны и часть P1/«оставлено» отпадает), или закрывать целевыми тулами?

## 8. Организационное
- **Милстоун:** новый **M5** (после M4 feature-complete). Forward-поток, не эпоха (онтология не
  меняется). План — `.planning/wiki/m5-plan.md`; таски — `.planning/wiki/tasks/m5-*/`.
- **Обновить intent:** F3 (`raw/features/F3-command-set.md`) — перенести реализованные пункты из
  «Out of scope (v2+)» в acceptance (AC3.9+), синхронно обновить `wiki/systems/command-set.md`
  (раздел «Core v1» → добить, §v2 — сократить). Атомарно raw+wiki+code (их правило).
- **Тесты:** EditMode по факту реализации (JSON-схема/роутинг новых тулов, `write_asset`
  round-trip + compile-error путь, `add/remove_component` изоляция, bulk `modify_object`
  транзакционность, multi-scene lifecycle через эмуляцию reload где применимо). Поведение, не
  приватное состояние; реальные `AssetDatabase`/`SessionState`/FS вместо моков.
- **README:** он устарел (заявляет M1 / 2 тула). Освежить каталог тулов заодно с M5.

## 9. Готовый промпт для агента-исполнителя (repo shtl-mcp)
> Реализуй милстоун **M5 — command-set v2 (murzak-parity для догфудинга)**, следуя
> forward-потоку CLAUDE.md (raw→wiki→code, атомарно, EditMode-тесты по факту). Источник
> требований и обоснований — `.planning/M5-murzak-parity-prompt.md`. Объём: **P0** обязательно
> (`write_asset`; `add_component`/`remove_component`; расширение `get_object`/`modify_object` —
> bulk + nested + target-по-ассету), **P1** по согласованию (`call_method`(+`find_method`);
> multi-scene `list/create/unload/set_active`; `screenshot camera`). Перед code-diff закрой
> «Открытые вопросы человеку» (§7) — не решай сам footgun-статус и именование. Обнови F3 +
> `systems/command-set.md` синхронно с кодом. Контракт новых тулов — как у встроенных
> (`ITool`, ручная `JObject`-схема, `NeedsMainThread`, структурированные ошибки, async-job для
> reload-спанящих). Не тащи в ядро §6 (материалы/шейдеры/packages/type-schema) — они остаются
> на escape hatches / host custom tools.
