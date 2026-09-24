# run_tests: Inconclusive неотличим от Passed

Дата: 2026-09-24. Исследование до фикса; фикс реализован — `TASK.md`, `journal.md`. Отличие от
плана ниже: массив имён назван `inconclusiveTests` (ключ `inconclusive` занят счётчиком).
Среда: Unity 2022.3.62f3, `com.unity.test-framework@1.1.33`, `com.unity.ext.nunit@1.0.6`
(engine-version 3.5.0.0), `TestProject~`, код на `main` @ `6fdbcf7`.

Повод: гейт тестов в host-проекте требует «0 failed, 0 inconclusive», evidence — counts из
`get_job`. Ревью заметило, что Inconclusive может выглядеть как Passed.

## Вывод

Да. Ответ `run_tests` → `get_job` не несёт ни счётчика, ни списка Inconclusive-тестов, а поле
`status` в двух измеренных сценариях возвращает `"Passed"` при наличии Inconclusive. По текущему
ответу гейт «0 inconclusive» проверить нельзя.

## 1. Код (что попадает в ответ)

`Editor/Tools/TestRunCallbacks.cs:90-97` — весь payload:

```csharp
["passed"] = result.PassCount,
["failed"] = result.FailCount,
["skipped"] = result.SkipCount,
["status"] = result.TestStatus.ToString(),
["failures"] = failures
```

- `result.InconclusiveCount` не читается нигде в `Editor/` (grep по `inconclusive` пуст).
- `status` — `TestStatus` **корня** дерева результатов, т.е. агрегат NUnit по сьютам, а не
  «худший из листьев».
- `Collect` (`TestRunCallbacks.cs:105-123`) берёт в `failures` только листья с
  `TestStatus.Failed` (`:115`); Inconclusive и Skipped в список не попадают.
- Job-статус (`done`/`failed`) от результатов тестов не зависит: `_jobs.Complete` (`:98`) при
  любом исходе; `failed` — только orphan-таймаут (`RunTestsTool.cs:227`).
- Описание тула (`RunTestsTool.cs:36`) обещает «passed/failed/skipped counts + failures».

Данные у адаптера есть: `TestResultAdaptor.cs:30` —
`InconclusiveCount = result.InconclusiveCount;` (Library/PackageCache, test-framework 1.1.33).

## 2. Документация Unity Test Framework

`TestStatus` ([docs 1.1](https://docs.unity3d.com/Packages/com.unity.test-framework@1.1/api/UnityEditor.TestTools.TestRunner.Api.TestStatus.html),
то же в XML-doc `UnityEditor.TestRunner/Api/TestStatus.cs`):

> The TestStatus enum indicates the test result status.
> Inconclusive — "The test ran with an inconclusive result."
> Skipped — "The test was skipped." · Passed — "The test ran and passed." · Failed — "The test ran and failed."

`ITestResultAdaptor` ([docs 1.1](https://docs.unity3d.com/Packages/com.unity.test-framework@1.1/api/UnityEditor.TestTools.TestRunner.Api.ITestResultAdaptor.html)):

> InconclusiveCount — "The number of test cases that were inconclusive when running the test and all its children."
> PassCount — "The number of test cases that passed when running the test and all its children."

NUnit ([Assert.Inconclusive](https://docs.nunit.org/articles/nunit/writing-tests/assertions/special-assertions/Assert.Inconclusive.html)):
> "The Assert.Inconclusive method indicates that the test could not be completed with the data available."

NUnit ([Assumptions](https://docs.nunit.org/articles/nunit/writing-tests/Assumptions.html)):
> "an unmet assumption will produce an `Inconclusive` test result, as opposed to a `Failure`."

Документация **не описывает**, как статус сьюта агрегируется из детей — это установлено только
измерением (ниже).

## 3. Эксперимент

Временная сборка `TestProject~/Assets/InconclusiveProbe` (удалена после прогона; исходник —
в конце страницы): три фикстуры, `ProbeRunner` через `-executeMethod` вызывает **реальные**
`RunTestsTool.Invoke` → `GetJobTool.Invoke` на `JobStore` живого сервера (рефлексией
`ShtlMcpServer._jobs`, чтобы `SweepOrphan` видел job) и параллельно пишет сырые поля корневого
`ITestResultAdaptor` отдельным `ICallbacks`.

```
Unity -batchmode -projectPath TestProject~ -executeMethod InconclusiveProbe.ProbeRunner.Run
```

Прогонов: 2, результаты совпали (без учёта jobId). exit=0 оба раза.

| Фикстура | `get_job.result` | Корневой адаптер |
|---|---|---|
| Mixed: Passes + `Assert.Inconclusive` | `passed 1, failed 0, skipped 0, status "Passed", failures []` | `TestStatus Passed, InconclusiveCount 1` |
| OnlyInconclusive: один `Assert.Inconclusive` | `passed 0, failed 0, skipped 0, status "Passed", failures []` | `TestStatus Passed, InconclusiveCount 1` |
| Ignore: Passes + `[Ignore]` | `passed 1, failed 0, skipped 1, status "Skipped", failures []` | `TestStatus Skipped, ResultState Skipped:Ignored, SkipCount 1` |

Независимая сверка — CLI `-runTests -testPlatform EditMode -assemblyNames InconclusiveProbe
-testResults …` (1 прогон, exit=2): XML NUnit показывает `inconclusive="2" passed="2"
skipped="1"`, листья `result="Inconclusive"`, фикстура `OnlyInconclusiveFixture` —
`result="Passed"`. То есть «Passed» у сьюта из одних Inconclusive — поведение самого NUnit 3.5
(Unity custom), а не shtl-mcp. Причину exit=2 CLI в этом прогоне не изолировал (в сборке
были и Ignored, и Inconclusive) — к вопросу не относится.

## 4. Разделение уровней

- **Измерено:** таблица выше (2 прогона пробы + 1 CLI).
- **Подтверждено документацией:** `TestStatus` имеет отдельное значение `Inconclusive`;
  адаптер отдаёт `InconclusiveCount`; unmet `Assume.That` → Inconclusive.
- **Выведено:** Inconclusive-тест в ответе `get_job` невидим полностью — не в счётчиках,
  не в `failures`, а `status` (агрегат NUnit) даёт `Passed`. Skipped/Ignored видимы через
  `skipped` и `status`, но их имён в ответе нет. Агрегацию сьюта по документации не проверить;
  опираюсь на измерение и XML.

## 5. План фикса (не начат)

Изменение наблюдаемого контракта `run_tests` → forward-поток: raw (F3, AC3.5 или новый AC) →
wiki (`systems/command-set.md`, строка `run_tests`) → код → CHANGELOG/релиз, одним PR.

Основание для нового поля — постановка задачи: «план фикса (счётчик inconclusive в ответе,
влияние на статус job)».

1. `TestRunCallbacks`: вынести сборку payload в `internal static JObject BuildPayload(ITestResultAdaptor root)`
   (паттерн `RunTestsTool.DecideScenePolicy` — чистая функция под EditMode-тест).
2. Payload: `["inconclusive"] = root.InconclusiveCount`.
3. **Решение человека:** список имён Inconclusive-листьев (новый массив `inconclusive`
   `{name, message}` рядом с `failures`) — без него счётчик говорит «есть», но не «какой».
   Рекомендую добавить.
4. **Решение человека:** `status`. Рекомендую вычислять из счётчиков, худший из
   `Failed > Inconclusive > Skipped > Passed`, вместо корневого агрегата NUnit, который прячет
   Inconclusive. Альтернатива — оставить NUnit-агрегат и описать, что гейт строится по счётчикам.
5. Job-статус не трогать: `done`/`failed` — исход выполнения job, упавшие тесты уже сейчас дают
   `done`; иначе клиенты перестанут получать `result` с деталями.
6. `RunTestsTool.Description`: добавить inconclusive в перечень.

| Сценарий | Старое | Новое (рекомендация 2–4) |
|---|---|---|
| Все Passed | `status Passed` | `status Passed, inconclusive 0` |
| Passed + Inconclusive | `status Passed`, inconclusive не видно | **[!]** `status Inconclusive, inconclusive 1` + имя |
| Только Inconclusive | `status Passed`, все счётчики 0 | **[!]** `status Inconclusive, inconclusive 1` + имя |
| Passed + Ignored | `status Skipped, skipped 1` | без изменений (+ `inconclusive 0`) |
| Есть Failed | `status Failed` (не измерялось в этой пробе) | `status Failed` |
| job-статус | `done` | `done` (без изменений) |

**[!]** — меняется наблюдаемый ответ `get_job`.

**Тест** (EditMode, `Tests/Editor/TestRunPayloadTests.cs`): фейковый `ITestResultAdaptor`
(интерфейс Unity — граница, не внутренний мок) с деревом Passed + Inconclusive-лист, корень
`TestStatus Passed, InconclusiveCount 1` — ровно измеренная форма. Ассерты:
`inconclusive == 1`, имя листа в списке, `status == "Inconclusive"`. RED-gate: на текущем коде
ключа `inconclusive` нет → падает. Настоящую Inconclusive-фикстуру в `Tests/` не класть —
self-suite перестанет быть зелёным. Сквозная проба (сборка из раздела 3) — ручная верификация
перед PR.

**Ветка:** `fix/run-tests-inconclusive` от `main` (@ `6fdbcf7`); в рабочем дереве `main` есть
незакоммиченная правка `.planning/wiki/tasks/create-asset/journal.md` — не смешивать.

**До фикса, в host-проекте:** `status`/counts из `get_job` не доказывают «0 inconclusive».
Evidence до фикса — `-runTests … -testResults` XML (`inconclusive="0"`) или grep по
`Assert.Inconclusive`/`Assume.` в тестах (последнее — эвристика, не доказательство).

<details><summary>Исходник пробы</summary>

`InconclusiveProbe.asmdef`: references `Shtl.Mcp.Tools`, `Shtl.Mcp.Dispatcher`,
`Shtl.Mcp.Lifecycle`, `UnityEngine.TestRunner`, `UnityEditor.TestRunner`; precompiled
`nunit.framework.dll`, `Newtonsoft.Json.dll`; Editor-only; `UNITY_INCLUDE_TESTS`.

```csharp
public class MixedFixture
{
    [Test] public void Passes() { Assert.IsTrue(true); }
    [Test] public void Inconclusive() { Assert.Inconclusive("probe inconclusive"); }
}
public class OnlyInconclusiveFixture
{
    [Test] public void Inconclusive() { Assert.Inconclusive("probe inconclusive only"); }
}
public class IgnoreFixture
{
    [Test] public void Passes() { Assert.IsTrue(true); }
    [Test, Ignore("probe ignored")] public void Ignored() { }
}
```

`ProbeRunner.Run`: `jobs = ShtlMcpServer._jobs` (рефлексия) → `new RunTestsTool(jobs)` /
`new GetJobTool(jobs)`; отдельный `ICallbacks.RunFinished` пишет `TestStatus`, `ResultState`,
`Pass/Fail/Skip/InconclusiveCount` корня; для каждой фикстуры
`run_tests {mode: EditMode, assembly: InconclusiveProbe, filter: "InconclusiveProbe\.<Fixture>\."}`,
опрос `get_job` в `EditorApplication.update` до `status != running`, JSON в файл
`$INCONCLUSIVE_PROBE_OUT`, `EditorApplication.Exit`.

</details>
