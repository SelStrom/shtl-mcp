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

### Command-set v2 (M5 — murzak-parity для догфудинга)

Расширения по итогам аудита замены murzak в боевом проекте (PerfectWar): частые
операции, для которых escape hatch объективно неудобен (`run_csharp` — footgun,
по умолчанию выключен). Философия тонкого ядра сохраняется: только частые
операции, длинный хвост остаётся на escape hatches (см. Out of scope).

- **AC3.9** — `write_asset`: создание/перезапись текстового ассета по пути под
  `Assets/` (`.cs`, `.uxml`, `.uss`, `.json`, `.asmdef`, …) — парный к `read_asset`.
  `refresh` (default true) импортирует записанное; для компилируемых расширений
  (`.cs`/`.asmdef`/`.asmref`) — async-job как у `refresh_assets`: `jobId`, ошибки
  компиляции доставляются через `get_job` (файл/строка/сообщение). **Обычный тул,
  не footgun** (решение человека): запись кода отделена от ad-hoc исполнения
  (`run_csharp`); модель и так пишет код через файловую систему.
- **AC3.10** — `add_component` / `remove_component`: жизненный цикл компонента
  GameObject — дополняет `modify_object` (тот пишет свойства существующего
  компонента, но не создаёт/не удаляет сам компонент). Тип — по полному имени;
  тип не найден → структурированная ошибка со списком-подсказкой; конфликты
  (`DisallowMultipleComponent`, удаление required по `RequireComponent`) —
  внятная ошибка, не глотать.
- **AC3.11** — расширение `get_object`/`modify_object`: (а) `modify_object`
  принимает массив изменений и вложенные пути свойств (`m_Size.x`) в одной
  транзакции `SerializedObject`; (б) target — не только scene-GO, но и Unity
  Object по asset-path или instanceId (ScriptableObject, материал, конфиг);
  (в) `get_object` отдаёт компоненты и сериализованные свойства достаточно
  глубоко для верификации состояния; (г) `modify_object` пишет **ссылочные поля**
  (`ObjectReference`) — значение задаётся той же ссылкой, что и `target`
  (scene-GO по пути/имени, asset-path, instanceId), `null` снимает ссылку.
  Без этого модель не может связать ассеты между собой (материал на рендерер,
  префаб в поле конфига) и вынуждена держать Editor-скрипты в host-проекте.
  Поле ждёт компонент, а указан GameObject → берётся компонент нужного типа
  с этого объекта; неподходящий тип → структурированная ошибка (запись
  не применяется, транзакция откатывается). Встроенные ресурсы Unity
  (`Library/unity default resources`) остаются вне scope — они не адресуются
  ни путём, ни стабильным id. Ссылку на объект сцены в поле ассета записать
  нельзя — это ограничение сериализации Unity, а не тула.
- **AC3.12** — `call_method` + `find_method` (reflection): вызов существующего
  C#-метода (static/instance, включая private) по типу и сигнатуре;
  `find_method` возвращает найденные сигнатуры для выбора перегрузки.
  **Обычный тул, не footgun** (решение человека): вызов существующего метода
  безопаснее компиляции произвольного кода.
- **AC3.13** — multi-scene: `list_scenes` (открытые сцены: path / isLoaded /
  isActive / isDirty), `create_scene`, `unload_scene`, `set_active_scene` —
  аддитивные многосценовые сценарии поверх `open_scene`/`save_scene`.
- **AC3.14** — `screenshot` принимает `camera` (имя/путь GameObject с `Camera`):
  кадр конкретной камеры, не только `game` (Camera.main) / `scene`.
- **AC3.15** — `create_asset`: создание **бинарного** ассета
  (`AssetDatabase.CreateAsset`) — материал (обязателен `shader`), наследник
  `ScriptableObject`, прочие `UnityEngine.Object` с конструктором без параметров.
  Парный `write_asset` покрывает только текстовые файлы, а такой ассет руками не
  собрать: без этого модель, которой нужен новый материал, вынуждена держать
  Editor-скрипт в host-проекте. Тонкое ядро сохраняется: тул только **создаёт**
  ассет, поля правит `modify_object` (AC3.11). Существующий путь → ошибка, если
  не передан `overwrite`. Типы, требующие аргументов конструктора (`Texture2D`,
  `RenderTexture`), — вне scope, как и материалы/шейдеры-специфика ниже.
- **AC3.16** — **prefab-stage как контекст работы**: пока открыт prefab-stage,
  объектные тулы (`get_hierarchy`, `find_gameobject`, резолв `target`/`parent` у
  `gameobject_*`, `set_parent`, `add_component`, `get_object`/`modify_object`,
  `instantiate_prefab`) работают по содержимому стейджа, а не по активной сцене.
  Так же ведёт себя сам Unity: пока стейдж открыт, Hierarchy показывает префаб,
  а объекты сцены недостижимы. Без этого редактирование префаба через MCP
  невозможно в принципе — тулы отвечают `target not found` на объект, который
  модель только что увидела в `get_selection`, и она вынуждена держать в
  host-проекте Editor-скрипт на `LoadPrefabContents` + `SerializedObject`.
  Ответы `get_hierarchy`/`find_gameobject`/`instantiate_prefab` несут поле
  `context` (`scene` | `prefabStage` + путь ассета), чтобы пустой результат
  читался как «искал не там», а не «объекта нет». `instantiate_prefab` принимает
  `parent` (объект текущего контекста) и сохраняет авторский локальный трансформ
  префаба — для UI сохранение мирового вместо локального ломает якоря
  `RectTransform`.

## Out of scope (v2+, достижимо через escape hatches)
- Профайлер, packages CRUD, материалы/шейдеры-специфика, isolated-screenshot,
  `type-get-json-schema`, built-in-ресурсы.
