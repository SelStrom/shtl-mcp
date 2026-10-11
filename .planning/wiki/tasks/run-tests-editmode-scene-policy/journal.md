# journal — run-tests-editmode-scene-policy

## 2026-10-11 — фикс

Повод: host-проект на Unity 6 дважды за сессию вставал на модале «Save modified scenes?» во время
EditMode `run_tests` — сцену пачкал uGUI-префаб (Undo «Driving RectTransform») при смене разрешения
Game view и на domain reload. Источник модала — `SaveModifiedSceneTask` Test Framework 1.6.0
(`UnityEditor.TestRunner/TestRun/TaskList.cs`: задача стоит и в ветке EditMode), а guard
`ResolveDirtyScenesForPlayMode` вызывался только при `mode == "PlayMode"`.

Решение: guard вызывается для обоих режимов, переименован в `ResolveDirtyScenes` (internal — для
теста оболочки), тексты отказов «Test run aborted». `DecideScenePolicy` не менялся. Discard в EditMode
согласован с поведением Test Runner: несохранённое после прогона всё равно не восстанавливается.

Верификация: EditMode `TestProject~` (Unity 2022.3.62f3, batchmode) — 246/246, из них 2 новых теста
оболочки на реальной сохранённой сцене. RED-gate для снятия условия по режиму нет: условие живёт в
`Invoke`, который стартует настоящий прогон и не вызывается из теста.
