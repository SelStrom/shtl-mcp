# Journal — m2-run-tests (T7, append-only)

## [2026-06-24] исследование прайор-арта

Сабагент изучил CoderGamester/mcp-unity, IvanMurzak/Unity-MCP, CoplayDev/unity-mcp. Ключевое:
- Эталон — CoplayDev: job+polling, durable-job в SessionState, переподписка ICallbacks на [InitializeOnLoad],
  финализация из RunFinished независимо от caller'а, TestRunnerNoThrottle, orphan-детект, полный Filter.
- Reload-spanning: mcp-unity НЕ решает (анти-пример); IvanMurzak — prewarm+pending-resume; CoplayDev — durable.
- Test-framework dependency: НИ ОДИН не изолирует — все жёстко зависят. Прайор-арта по opt-in изоляции нет.
- Focus-throttle: только CoplayDev борется (TestRunnerNoThrottle); подтверждают, что входящий сокет-запрос
  будит главный поток (наш HttpListener — тот же эффект).

Выбор пользователя: **вариант A (жёсткая зависимость от com.unity.test-framework)**.

## [2026-06-24] реализация + headless-верификация

Сделано: RunTestsTool, TestRunCallbacks (RunFinished + ReattachIfPending), package.json + asmdef
(test-framework refs), проводка в ShtlMcpServer. Self-recompile автономно → компилируется чисто,
run_tests в tools/list.

Headless-прогоны (curl, автономно):
- run_tests filter=JobStore → job done {passed:5, status:Passed}.
- run_tests filter=FnvTests → job done {passed:3, status:Passed}.
Оба корректны. Маркер очищается, параллельные прогоны блокируются.

## [2026-06-24] находка: focus-throttle блокирует юзабилити в фоне

При unfocused-прогоне сервер неотзывчив всю длительность: get_job/status отдают временные DOWN
(curl-таймауты), т.к. editor update троттлится и листенер-поток голодает. reloadCount НЕ меняется
(reload нет — это голод, не падение). Прогон доводится (опросы = слабые будильники), но медленно:
JobStoreTests довёлся быстро, FnvTests — >180с DOWN затем done. Без частых внешних стимулов прогон
рискует стоять.

Вывод: self-test механически работает (результаты корректны), но для отзывчивого/быстрого прогона в
фоне НУЖЕН TestRunnerNoThrottle (next). Зафиксировано в TASK.md (AC-3, refinements).

## [2026-06-24] no-throttle + orphan + reload-spanning fix → AC-2/3/4 закрыты

**Исследование прайор-арта (сабагент, CoplayDev/unity-mcp main):** механизм no-throttle — НЕ pump
`QueuePlayerLoopUpdate()`, а две `EditorPrefs` (`ApplicationIdleTime=0` + `InteractionMode=1`),
впечатанные приватным `EditorApplication.UpdateInteractionModeSettings()` через рефлексию (load-bearing —
без него prefs не вступают в силу). Снимок prefs у CoplayDev только в SessionState. Orphan: init-timeout
15с + stale-after-reload 5мин. PlayMode: DisableDomainReload + диск-marker для enterPlayModeOptions.

**Реализация.** Новый `Editor/Tools/TestRunnerNoThrottle.cs`: Apply/Restore/RecoverOnLoad, **двухслойный**
бэкап (SessionState + `Library/ShtlMcpNoThrottleBackup.txt`). Расхождение с prior-art в плюс: диск-слой
для самих prefs (EditorPrefs durable → при крахе без него редактор навсегда в no-throttle и originals
потеряны; CoplayDev диск делает только для enterPlayModeOptions). `RunTestsTool`: preemptive Apply в Invoke
(в фоне RunStarted может не успеть тикнуть), nudge `QueuePlayerLoopUpdate`, единый static `_api` +
`DestroyImmediate` прошлого (guard дублей ICallbacks), `SweepOrphan` (orphan-таймаут 10мин, с тика
watchdog). `TestRunCallbacks`: Apply в RunStarted, Restore в RunFinished (внутри marker-guard →
идемпотентно). `ShtlMcpServer`: RecoverOnLoad в EnsureStarted, SweepOrphan в WatchdogTick. `AssemblyInfo.cs`
(InternalsVisibleTo для тест-хуков). Тесты: `TestRunnerNoThrottleTests` (round-trip, crash-recovery с диска
после потери SessionState, recover-on-reload-pending), `RunTestsOrphanTests` (stale/fresh/missing).

**Тест-изоляция (важно):** прогон через MCP run_tests УЖЕ применяет no-throttle и держит live-маркер,
поэтому тесты, мутирующие глобальное no-throttle-состояние / live-ключи, обязаны его полностью вернуть —
иначе RunFinished оставит редактор в no-throttle / осиротит job. Добавлен `TestRunnerNoThrottle.Snapshot/
RestoreSnapshot` (internal), брекетинг в OneTime-хуках обоих новых тест-классов.

**Находка на первом полном сьюте: reload-spanning терял job.** reloadCount 17→18 (ReloadSurvivalTests
форсит реальный reload), но `get_job` → unknown. Причина: `JobStoreTests`/`GetJobToolTests` стирают live-ключ
`Shtl.Mcp.Jobs` в SetUp/TearDown (бегут до ReloadSurvivalTests) → reload перезагружает JobStore из
опустошённого SS → live-job пропадает. НЕ production-баг (в реале ключ никто не стирает) — тест-гигиена.
**Корневой фикс:** `JobStore` получил инъектируемый `sessionKey` (дефолт = прод-ключ); JobStoreTests,
GetJobToolTests, RunTestsOrphanTests переведены на изолированные ключи → ноль коллизий с live.

**Верификация (headless, автономно, Unity НЕ в фокусе):**
- Чистый субсет (`NoThrottle|Orphan`, без reload): 7/7 passed, status_DOWN=0, max-lat 0.05с, job доехал.
- **Полный сьют reload-spanning: 52 passed / 0 failed / 0 skipped, status Passed.** reloadCount 17→18 —
  live-job пережил reload и доставлен `done` с результатом. status-латентность 0.05–0.18с весь прогон;
  единственный DOWN ≈2с = окно самого domain reload (норма по lifecycle §1), не троттл-DOWN. Прогон 7.5с
  (было >180с unfocused).
- Чистота после прогона: health=ok, mode=edit, disk-marker no-throttle удалён (footgun не оставлен),
  застрявших job/маркеров нет (новый run_tests стартует свободно; пустой filter → done за <1с).

Все три AC закрыты. Реализация существующего интента (F3/AC3.1+3.5, F4/AC4.2/4.5) — raw не менялся.
