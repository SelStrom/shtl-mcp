# run-tests-inconclusive — Inconclusive в результате run_tests

- **Статус:** done (ожидает ревью/мержа)
- **raw:** `F3-command-set.md` → AC3.5 (форма результата `run_tests`)
- **wiki:** `systems/command-set.md` (строка `run_tests`)
- **code:** `Editor/Tools/TestRunCallbacks.cs` (`BuildPayload`), `Editor/Tools/RunTestsTool.cs` (описание),
  `Tests/Editor/TestRunPayloadTests.cs`
- **исследование:** `notes.md`

## Цель

Результат `run_tests` → `get_job` не отличал Inconclusive от Passed: в ответе были только
passed/failed/skipped и `TestStatus` корня, который NUnit сворачивает в `Passed` и для сьюта
Passed + Inconclusive, и для сьюта из одних Inconclusive. Host-проект с гейтом «0 failed,
0 inconclusive» не мог проверить его по ответу.

## Acceptance

- В результате есть счётчик `inconclusive` и список `inconclusiveTests` (`{name, message}`).
- `status` — худший исход Failed > Inconclusive > Skipped > Passed; Passed + Inconclusive и
  только Inconclusive дают `Inconclusive`.
- Сбой уровня сьюта (корень `Failed` при `FailCount 0`, напр. `OneTimeTearDown`) остаётся `Failed`.
- Passed + Ignored — `Skipped`, как и раньше. Job-статус — `done` при любом исходе тестов.

## Вне scope

- Сообщение сбоя уровня сьюта в `failures` не попадает (листьев со статусом Failed нет) — так было
  и до фикса; `status: Failed` его сигнализирует.

## Шаги

1. `TestRunCallbacks.BuildPayload(ITestResultAdaptor)` — чистая функция (паттерн `DecideScenePolicy`).
2. Счётчик, список inconclusive-листьев, `status` по счётчикам + корень `Failed`.
3. EditMode-тесты на фейковом `ITestResultAdaptor` формы, снятой с реального прогона.
4. Сквозная проба временной сборкой в `TestProject~` (реальные `RunTestsTool` → `GetJobTool`).
