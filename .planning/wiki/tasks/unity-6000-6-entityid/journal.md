# journal — unity-6000-6-entityid

## 2026-10-11

- Повод: перевод host-проекта на 6000.6.5f1 — пакет падал с CS0619 на `GetInstanceID` и
  `InstanceIDToObject` в `ComponentTools`, `ReflectionTools`, `SceneEditTools`, `SceneObjectTools`.
- Первая попытка (`GetEntityId()` с приведением к int) упала на том же CS0619: приведение
  `EntityId -> int` тоже obsolete-error. Id отдаётся как `long` из 8 байт `EntityId`
  (`ToULong`/`FromULong`) — так же поступает upstream VContainer под `UNITY_6000_4_OR_NEWER`.
- Контракт JSON не меняется: `instanceId` и раньше был числом, `target` уже разбирался как `long`.
  На 6000.6 число может не помещаться в int — клиенту это безразлично, он передаёт его обратно как есть.
- Верификация, `TestProject~` (копия с пакетом по `file:`), EditMode:
  - 6000.6.5f1 — 244/244 passed, `error CS` 0;
  - 2022.3.62f3 — 244/244 passed.
