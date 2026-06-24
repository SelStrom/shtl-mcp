# TASK: T7 — run_tests (Test Runner → job)

**Status:** done (no-throttle + orphan-таймаут + reload-spanning e2e верифицированы headless)
**Привязка:** F3/AC3.1 (run_tests), F3/AC3.5 (⏳ job), F4/AC4.2 (job переживает reload). На фундаменте
T1 (`m2-async-job`). Системы: `command-set.md`, `lifecycle-and-reload.md`. **Цель — self-test
(автономный прогон тестов агентом).**

## Решения (по исследованию прайор-арта)

- Прайор-арт: CoderGamester/mcp-unity, IvanMurzak/Unity-MCP, **CoplayDev/unity-mcp** (эталон). См.
  исследование в журнале вехи / этом таске.
- **Зависимость от test-framework: вариант A (жёсткая)** — выбор пользователя. `run_tests` в основном
  `Shtl.Mcp.Editor` asmdef; пакет зависит от `com.unity.test-framework` (package.json + asmdef refs
  `UnityEditor.TestRunner`/`UnityEngine.TestRunner`). Прайор-арта по изоляции зависимости нет.
- Модель: **job + polling** (CoplayDev) — ложится на наш JobStore (T1).
- Reload-spanning: durable-маркер in-flight jobId в `SessionState` + переподписка `ICallbacks` после
  reload (`TestRunCallbacks.ReattachIfPending` из `EnsureStarted`) + финализация из `RunFinished`
  независимо от живости caller'а.

## Сделано

- `RunTestsTool` (`run_tests`): mode (EditMode/PlayMode), filter (groupNames regex), assembly. Создаёт
  job, ставит маркер `Shtl.Mcp.TestJob`, `TestRunnerApi.Execute`. Запрет параллельных прогонов.
- `TestRunCallbacks : ICallbacks`: `RunFinished` → собрать passed/failed/skipped + упавшие листья
  (имя+сообщение) → `JobStore.Complete`, очистить маркер. `ReattachIfPending` для reload-spanning.
- package.json + asmdef: зависимость от test-framework (вариант A).
- Проводка в `ShtlMcpServer` (`Jobs`-property, register, ReattachIfPending в EnsureStarted).

## Верифицировано (headless, автономно)

- Компилируется чисто; `run_tests` в `tools/list`.
- `run_tests filter=JobStore` → job `done` `{passed:5, status:Passed}`.
- `run_tests filter=FnvTests` → job `done` `{passed:3, status:Passed}`.
- Reload-survival маркера/job — через T1 JobStore (round-trip протестирован).

## Решено: focus-throttle (no-throttle) + reload-spanning потеря job

**Focus-throttle** (предсказан исследованием, Q6): unfocused → editor update троттлится → прогон ползёт,
листенер-поток голодает → `status`/`get_job` отдают DOWN всю длительность (FnvTests: >180с). **Фикс
(`TestRunnerNoThrottle`, паттерн CoplayDev):** на время прогона `EditorPrefs ApplicationIdleTime=0` +
`InteractionMode=1` («No Throttling»), впечатанные приватным `EditorApplication.UpdateInteractionModeSettings()`
(рефлексия, load-bearing). Применяется preemptive в `Invoke` (RunStarted может не успеть тикнуть в фоне) +
повторно в `RunStarted`; restore в `RunFinished` (внутри marker-guard → дубли колбэков не двойнят).
**Бэкап двухслойный:** SessionState (переживает reload) + marker-файл `Library/ShtlMcpNoThrottleBackup.txt`
(переживает краш — критично: `EditorPrefs` durable, при крахе без диск-слоя редактор навсегда в no-throttle
и исходные значения потеряны; CoplayDev этот слой для prefs НЕ делает — наше расхождение в плюс).
`RecoverOnLoad` из `EnsureStarted`: in-flight прогон → держим full-rate, осиротевший снимок → откат.

**Orphan-таймаут:** `RunTestsTool.SweepOrphan` с тика watchdog — running-job без завершения >10 мин (или
исчезнувший/финализированный с зависшим маркером) → авто-fail + снять маркер + вернуть троттлинг.

**Reload-spanning потеря job (всплыло на первом полном сьюте):** `JobStoreTests`/`GetJobToolTests`
стирали live-ключ `Shtl.Mcp.Jobs` в SetUp/TearDown, а `ReloadSurvivalTests` форсит reload → JobStore
перезагружался из опустошённого SS → live-job пропадал (get_job → unknown). НЕ production-баг (в реале
ключ никто не стирает) — тест-гигиена. **Фикс:** `JobStore` получил инъектируемый `sessionKey` (дефолт
= прод-ключ), тесты используют изолированные ключи → ноль коллизий с live-прогоном.

## Остаток (refinements, не блокеры)

- Дубли подписок `ICallbacks`: единый static `_api` + `DestroyImmediate` прошлого перед новым + marker-guard
  в `RunFinished` → финализация идемпотентна. (Закрыто на уровне корректности.)
- Прогресс-стриминг в job (completed/total, текущий тест) — опц., не сделано (orphan = wall-clock от старта).
- INV-3 identity-инъекция в ответы (общий долг M2) — не сделано.
- PlayMode: `DisableDomainReload` + бэкап enterPlayModeOptions (CoplayDev) — отдельно, не сделано.
- `clear_stuck` как явный инструмент — не сделано (SweepOrphan покрывает авто; ручной — при нужде).

## Acceptance

- AC-1: run_tests EditMode → job с passed/failed/skipped + failures. ✅ (headless, full suite 52/52)
- AC-2: переживает reload (durable маркер + reattach). ✅ — полный e2e: reloadCount 17→18 (ReloadSurvivalTests
  форсит reload), live-job пережил и доехал `done` с результатом (после фикса изоляции ключа).
- AC-3 (no-throttle): прогон в фоне НЕ подвешивает сервер; status отзывчив всё время. ✅ — латентность
  0.05–0.18с весь прогон; единственный DOWN ≈2с = легитимное окно самого reload, не троттл. Прогон 7.5с
  (было >180с unfocused).
- AC-4: self-test — агент сам гоняет тесты и читает результат. ✅ — полный сьют headless, 52/52, результат читается.
