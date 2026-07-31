# prefab-stage-context — стейдж как контекст работы объектных тулов

**raw:** F3/AC3.16 · **wiki:** [[command-set]] · **статус:** done

## Цель

Пока открыт prefab-stage, объектные тулы должны адресовать его содержимое, а не активную сцену —
как это делает сам Unity (Hierarchy показывает префаб, объекты сцены недостижимы).

## Повод

`GraphicsApiUiBuilder.cs` в MadOut2 — Editor-скрипт на 934 строки, написанный **только** ради того,
что через MCP нельзя развести внутрипрефабные ссылки. Симптом со стороны модели: `get_selection`
возвращает `Canvas (Environment)/GraphicsApi_SelectionPanel`, а `get_object` на тот же путь отвечает
`target not found`. Ровно тот же класс повода, что у `create_asset` (AC3.15).

## Acceptance

1. При открытом стейдже `get_hierarchy` отдаёт дерево префаба, `find_gameobject` ищет в нём, объекты
   активной сцены не находятся; после закрытия — обратно сцена.
2. Созданное через `gameobject_create` внутри стейджа принадлежит `stage.scene`.
3. `instantiate_prefab` принимает `parent` (объект текущего контекста), сохраняет авторский локальный
   трансформ префаба и возвращает `path` инстанса.
4. Ответы `get_hierarchy` / `find_gameobject` / `instantiate_prefab` несут `context`
   (`scene` | `prefabStage` + `prefabPath`).
5. `close_prefab` не открывает модальный диалог на грязном стейдже.
6. EditMode-набор зелёный.

## Шаги

1. `SceneObjects.Roots()` → `TargetScene()` (стейдж, иначе активная сцена) — единственная точка знания,
   резолв по пути/имени, обход и создание переключаются вместе.
2. `SceneObjects.ContextInfo()` + отдача `context` из трёх тулов.
3. `instantiate_prefab`: `parent` + создание в `TargetScene()`.
4. `close_prefab`: политика `discard` (по умолчанию) | `save` | `abort`, снятие dirty-флага перед
   `GoToMainStage()`.
5. Тесты `PrefabStageContextTests` на реальном стейдже.

## Границы

Не трогаем: `save_scene` при открытом стейдже (для префаба есть `save_prefab`), вложенные стейджи
(Unity держит стек — берём верхний, как `GetCurrentPrefabStage`), выбор «работать по сцене вопреки
открытому стейджу» (явного оверрайда нет; закрыть стейдж — это `close_prefab`).
