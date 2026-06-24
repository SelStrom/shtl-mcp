# TASK: T5 — prefabs

**Status:** done (5 тулов; 66/66, prefab round-trip на реальном PrefabUtility/PrefabStage)
**Привязка:** F3/AC3.1 (тулсет). Размещение — `Editor/Tools/PrefabTools.cs` (после T2).

## Тулы (все main-thread, Editor-only)

| Тул | Что |
|---|---|
| `create_prefab` | prefab-ассет из объекта сцены (`from` по имени) либо новый пустой (root = basename файла) |
| `open_prefab` | открыть в изолированном prefab-stage (`PrefabStageUtility.OpenPrefab`) |
| `save_prefab` | сохранить открытый stage (`SaveAsPrefabAsset(prefabContentsRoot, assetPath)`) |
| `close_prefab` | закрыть stage (`StageUtility.GoToMainStage`) |
| `instantiate_prefab` | инстанцировать ассет в активную сцену (`PrefabUtility.InstantiatePrefab`) |

## Заметки

- Имя корня пустого prefab Unity задаёт по **basename файла** (не по arg `name`) — учтено в тесте/доках.
- Нет открытого stage → `save_prefab`/`close_prefab` отдают понятную ошибку/`note` (AC4.5).

## Верификация

- Integration (`PrefabToolsTests`, 3) на РЕАЛЬНОМ PrefabUtility/PrefabStage: round-trip create(пустой)→open
  (stage открыт, root)→save→close(stage закрыт)→instantiate(инстанс в сцене); save без stage → error;
  instantiate несуществующего → error. Чистится в TearDown. ✅
- Полный сьют: **66/66 passed** (63 + 3).

## Долги

- Редактирование содержимого открытого prefab (добавить компоненты/детей) — через T6 (scene-objects,
  работающие с `prefabContentsRoot`); сейчас open/save/close — каркас stage.
- INV-3 identity-инъекция — общий долг M2.
