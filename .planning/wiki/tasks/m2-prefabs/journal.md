# Journal — m2-prefabs (T5, append-only)

## [2026-06-24] реализация + фикс теста

5 тулов в `PrefabTools.cs`: create_prefab (SaveAsPrefabAsset из scene-объекта или нового пустого),
open/save/close (PrefabStageUtility/StageUtility), instantiate (PrefabUtility.InstantiatePrefab +
MarkSceneDirty). Все main-thread, регистрация в ShtlMcpServer.

Первый прогон: 65/66, упал `Prefab_RoundTrip` — open вернул root=basename файла («ShtlMcpT5Tmp»), а тест
ждал arg `name` («ShtlMcpT5Root»). Находка: Unity именует корень ПУСТОГО prefab по basename ассета, а не
по имени temp-GameObject. Не баг тула — выровнял константы теста (basename == RootName), зафиксировал в
TASK.md/доках. Также TearDown искал инстанс по неверному имени — исправлено тем же выравниванием.

Верификация: 66/66 (3 prefab-теста). Round-trip на реальном PrefabUtility/PrefabStage (не моки):
create→open→save→close→instantiate, плюс ошибки (save без stage, instantiate несуществующего). Реализация
F3/AC3.1 — raw не менялся.
