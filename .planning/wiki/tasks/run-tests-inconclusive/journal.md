# journal — run-tests-inconclusive

## 2026-09-24 — исследование

Дефект подтверждён кодом и пробой — подробно в `notes.md`. Ключевое измерение: сьют из одного
`Assert.Inconclusive` → корень `TestStatus Passed`, в ответе `passed 0, failed 0, skipped 0,
status "Passed"`; `InconclusiveCount 1` у адаптера есть, но не читался.

## 2026-09-24 — фикс

Решения (рекомендации из `notes.md` §5, приняты запросом «сделай фикс»): список имён + `status` по
счётчикам. Имя массива — `inconclusiveTests`: `inconclusive` занят счётчиком.

Отклонение от плана: гард «корень `Failed` → `Failed`». Худший-из-счётчиков потерял бы сбой уровня
сьюта — проба с `[OneTimeTearDown]`, бросающим исключение, дала корень `Failed(Child)` при
`FailCount 0, PassCount 1`; без гарда ответ стал бы `Passed`.

## Верификация

Unity 2022.3.62f3, `TestProject~`, CLI `-batchmode -runTests -testPlatform EditMode`:

- RED-gate: `BuildPayload` временно со старой семантикой (`status` = корень, без счётчика и списка),
  `-testFilter TestRunPayloadTests` → 5 total, **4 failed**, 1 passed (`SuiteLevelFailure…` —
  страж новой логики, на старой семантике зелёный ожидаемо). exit=2.
- Полный EditMode-сьют на фиксе: **225 passed / 0 failed / 0 inconclusive / 0 skipped**, exit=0
  (было 220, +5 кейсов).
- Сквозная проба (временная сборка `InconclusiveProbe`, удалена; реальные `RunTestsTool` →
  `GetJobTool` на `JobStore` сервера), результат `get_job`:

| Фикстура | status | passed/failed/skipped/inconclusive | списки |
|---|---|---|---|
| Passes + Inconclusive | Inconclusive | 1/0/0/1 | inconclusiveTests: 1 |
| только Inconclusive | Inconclusive | 0/0/0/1 | inconclusiveTests: 1 |
| Passes + Ignore | Skipped | 1/0/1/0 | — |
| Fails + Inconclusive | Failed | 0/1/0/1 | failures: 1, inconclusiveTests: 1 |
| Passes + OneTimeTearDown throw | Failed | 1/0/0/0 | — |

## 2026-09-24 — ребейз на main (0.10.0)

Ветка перенесена на `origin/main` @ `693eb79` (prefab-stage, sticky serverName, set_selection).
Конфликты — только `package.json`, `CHANGELOG.md`, `log.md`; релиз переименован 0.8.1 → 0.10.1.
Код run_tests на main не менялся. Полный EditMode-сьют после ребейза: **244 passed / 0 failed /
0 inconclusive / 0 skipped**, exit=0. Сквозная проба не повторялась — код фикса идентичен.
