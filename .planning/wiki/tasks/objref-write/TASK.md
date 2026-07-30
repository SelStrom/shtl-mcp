# objref-write — запись ссылочных полей в modify_object

- **Статус:** done (ожидает ревью/мержа)
- **raw:** `F3-command-set.md` → AC3.11 (г)
- **wiki:** `systems/command-set.md` (строка `get_object`/`modify_object`)
- **code:** `Editor/Tools/SceneEditTools.cs` (`SerializedValues`), `Tests/Editor/SceneEditToolsTests.cs`

## Цель

`modify_object` писал скаляры, векторы, цвета и enum'ы, но на ссылочном поле возвращал
`unsupported property type for write: ObjectReference`. Из-за этого модель не могла связать
ассеты между собой — назначить материал рендереру, положить префаб в поле конфига, заполнить
authoring-компонент в сцене — и была вынуждена держать в host-проекте Editor-скрипты,
существующие только ради `SerializedProperty.objectReferenceValue`.

Повод — реальная задача в PerfectWar (T-0064, миграция на Entities Graphics): два
Editor-инструмента (`SetupSessionPrefabRefsTool`, `CreateLootGhostPrefabsTool`) написаны
исключительно потому, что MCP не умеет писать ссылки.

## Acceptance

- Значение ссылочного поля задаётся той же формой, что и `target`: путь/имя scene-GO,
  asset-path, instanceId.
- `null` снимает ссылку.
- Поле ждёт компонент, а указан GameObject → берётся компонент нужного типа с этого объекта.
- Неподходящий тип → структурированная ошибка; транзакция `modify_object` откатывается целиком.
- Несуществующий путь → ошибка резолва (`no asset at path: …`), запись не выполняется.

## Вне scope

- Встроенные ресурсы Unity (`Library/unity default resources`, примитивные меши) — не
  адресуются ни путём, ни стабильным id.
- Sub-assets (Sprite внутри png, Mesh внутри FBX) — резолвится только main-asset.
- Ссылка на объект сцены в поле ассета — Unity её не сериализует, это ограничение движка.

## Шаги

1. `SerializedValues.Write` → сигнатура с `out string error` (раньше `bool` без причины отказа).
2. Ветка `ObjectReference`: резолв значения через существующий `ObjectRefs.Resolve`,
   определение ожидаемого типа поля из `SerializedProperty.type` (`PPtr<$Type>`) через
   `TypeResolve.Find`, подбор компонента с GameObject, проверка типа.
3. Ошибка из `Write` пробрасывается в ответ тула вместо прежнего общего текста.
4. EditMode-тесты: asset-path + очистка null, scene-GO → компонент, mismatch (с проверкой
   транзакционности), отсутствующий ассет.
