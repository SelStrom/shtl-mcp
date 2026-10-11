# unity-6000-6-entityid — компиляция на Unity 6000.6

- **Статус:** done (ожидает ревью/мержа)
- **raw:** — (совместимость, поведение тулов не меняется)
- **wiki:** —
- **code:** `Editor/Tools/SceneObjectTools.cs` (`SceneObjects.IdOf` / `ObjectById`), `ComponentTools.cs`,
  `ReflectionTools.cs`, `SceneEditTools.cs`; тесты `ReflectionToolsTests.cs`, `SceneEditToolsTests.cs`

## Цель

На Unity 6000.6 пакет не компилируется: `Object.GetInstanceID()`, `EditorUtility.InstanceIDToObject(int)`
и неявные приведения `EntityId <-> int` помечены `[Obsolete(error)]` (CS0619). Host-проект на 6000.6
получает ошибки компиляции в пакете и уходит в Safe Mode.

## Acceptance

- Пакет и его тесты (`SHTL_MCP_DEV`) компилируются на 6000.6 и на 2022.3.
- `instanceId` в ответах тулов и `target` по числу делают round-trip на обеих версиях.
- EditMode в `TestProject~` зелёный на обеих версиях.

## Шаги

1. Две точки перевода id в `SceneObjects`: на `UNITY_6000_4_OR_NEWER` — 8 байт `EntityId`
   (`EntityId.ToULong` / `FromULong`, `EditorUtility.EntityIdToObject`), иначе `GetInstanceID` /
   `InstanceIDToObject`.
2. Все выдачи `instanceId` и разбор `target` — через них; тесты — тоже.
