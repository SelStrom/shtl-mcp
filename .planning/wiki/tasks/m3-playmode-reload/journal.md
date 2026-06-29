# Journal — m3-playmode-reload (T5, append-only)

## [2026-06-25] реализация
PlayModeOptionsGuard — структурная копия TestRunnerNoThrottle (двухслойный бэкап SessionState+диск,
Apply/Restore/RecoverOnLoad), но для `EditorSettings.enterPlayModeOptions |= DisableDomainReload`. Хуки:
RunTestsTool(mode=PlayMode)→Apply, RunFinished→Restore (идемпотентно для EditMode), EnsureStarted+Bootstrap
(disabled)→RecoverOnLoad. Unit-тесты (4) зеркалят no-throttle-тесты, снимок реальных EditorSettings в OneTime.
92/92. Реальный PlayMode-прогон e2e отложен (нет PlayMode-тестов в TestProject; это инфраструктура для
PlayMode-тестов пользователя). Реализация F4 — raw не менялся.

## [2026-06-26] закрытие e2e-пробела + dirty-scene guard (modal-free для PlayMode)

**Контекст:** при попытке реально прогнать PlayMode дважды зависал главный поток. Причина (через `ping`:
`responsive=False`, mode=edit, не compiling) — Unity **Test Runner** перед PlayMode-прогоном показывает
блокирующий «Save scene?» модал, если активная сцена грязная. Это единственный источник автономного модала,
не покрытый аудитом T1 (там — про scene-**тулы**, а тут — собственный flow Test Runner'а). MCP такой модал
не закроет (главный поток в modal-loop). orphan-свип (10 мин) корректно авто-зафейлил повисший job.

**Фикс — guard в `RunTestsTool.Invoke` (PlayMode ветка, до создания job):** активная сцена грязная →
сцену **с путём** сохраняем молча (`EditorSceneManager.SaveScene`) → Test Runner не промптит; **untitled**
(без пути) → ранний **отказ с понятной ошибкой** ВМЕСТО модала («save it first… otherwise Test Runner opens
a blocking 'Save scene?' dialog that wedges the MCP»). Это и есть modal-free (AC4.9) для PlayMode: либо
сохраняем сами, либо отказываем, но модал не всплывает.

**E2E-верификация (TestProject~/Assets/PlayModeTests, минимальный `[UnityTest] Enters_Play_And_Passes`):**
после `save_scene` (чистая сцена) `run_tests mode=PlayMode` → `done, passed:1, status:Passed`; `ping`
оставался `responsive=True age=0.0` весь прогон (без зависания); `reloadCount` 81→81 без изменений —
**DisableDomainReload-guard подтверждён** (вход в Play не вызвал domain reload). Регресс: 94/94 EditMode
зелёные после правки RunTestsTool.

## [2026-06-26] code-review M3 (3 агента: reliability/correctness/security) + ремедиация

Прогон 3 ревью-агентов по M3-changeset (`113b82a..HEAD` + рабочее дерево). **BLOCKER'ов нет.** Две MAJOR
и набор MINOR; исправлено (решения автономно):

- **MAJOR (correctness): silent-save vs AC4.9.** Первый guard безусловно `save`-ил грязную сцену — расхождение
  с дефолтной политикой `discard` и без выбора LLM. Переделано в **`scenePolicy`** (`discard` дефолт по AC4.9 /
  `save` / `abort`), modal-free, на ВСЕ загруженные сцены; untitled и multi-scene → ранний отказ-ошибка вместо
  модала. Ветвление вынесено в чистый `DecideScenePolicy` (10 unit-тестов, `RunTestsScenePolicyTests`). Это
  реализация уже существующего намерения AC4.9 — raw не менялся. E2e: грязная сцена + PlayMode + discard →
  `passed:1`, ping responsive, сцена вернулась к clean (объект отброшен перечитыванием).
- **MAJOR (reliability): JobStore.Get torn-read.** `Get` отдавал живую ссылку → `GetJobTool` читал поля с
  фонового потока вне lock. Теперь `Get` возвращает `Job.Clone()` под lock (согласованный снимок). Тест
  `Sweep_FailsStaleRunningJob` бэкдейтил через живую ссылку → добавлен явный seam `JobStore.BackdateForTest`.
- **MINOR**: `SweepOrphan` теперь зовёт `PlayModeOptionsGuard.Restore()` симметрично no-throttle; `recoveryHint`
  добавлен в image/`_content`-ветку McpRouter (AC7.3 «каждый ответ»); dashboard-детект «configured» — с substring
  на token-boundary (`NameListed`, regex по началу строки) против false-positive дедуп-имён; wiki
  [[recovery-discoverability]] §Канал 1 выровнен под факт (registry — массив, `recovery` per-instance).
- **Security**: реальных уязвимостей нет (loopback-only + Host/Origin fail-closed, footgun-гейт соблюдён,
  спавн `claude` без инъекции). Добавлен регресс-тест фильтра — `IsRequestAllowed` вынесен в чистый метод,
  `HttpServerFilterTests` (8 кейсов: loopback/localhost ok, чужой/пустой Host, любой Origin, неверный порт).
- **Отклонено**: «recoveryHint только в errors» (противоречит AC7.3); реинициализация `_lastDrainTicks`
  (защищённый компромисс — текущий init предотвращает ложно-гигантский age).

Итог: **112/112 EditMode зелёные**, PlayMode e2e (clean + dirty-discard) зелёный, компиляция чистая.
