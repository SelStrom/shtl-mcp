# run-tests-editmode-scene-policy — scenePolicy и для EditMode-прогона

- **Статус:** done (ожидает ревью/мержа)
- **raw:** `F4-play-edit-reload.md` → AC4.9 (modal-free scene ops) — намерение не меняется, фикс его нарушения
- **wiki:** без изменений (политика в systems не описана построчно)
- **code:** `Editor/Tools/RunTestsTool.cs` (`ResolveDirtyScenes`, описание `scenePolicy`),
  `Tests/Editor/RunTestsScenePolicyTests.cs`

## Цель

`run_tests` разрешал грязную сцену по `scenePolicy` только для PlayMode. Test Framework 1.6 (Unity 6)
ставит `SaveModifiedSceneTask` → `EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo` и в
список задач EditMode (`TaskList.GetTaskList`), поэтому EditMode-прогон при грязной сцене открывал
модал «Save modified scenes?», и главный поток стоял до клика человека — нарушение AC4.9.

## Acceptance

- `scenePolicy` (discard по умолчанию / save / abort) применяется до старта прогона любого режима.
- Тексты отказов не привязаны к PlayMode.
- Оболочка на реальной сцене: discard перечитывает грязную сцену с диска, abort оставляет её грязной
  и возвращает ошибку.

## Вне scope

- Живая проба модала в GUI-редакторе: `TestProject~` на Unity 2022.3 с Test Framework 1.1.33, а дефект
  снят на Unity 6 / 1.6.0 в host-проекте; проверка — после бампа пакета там.
