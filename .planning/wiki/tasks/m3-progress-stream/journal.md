# Journal — m3-progress-stream (T6, append-only)

## [2026-06-25] реализация
Прогресс run_tests в job: Job.Completed/Total/CurrentTest (in-memory, без персиста); JobStore.SetProgress
(только running, без Persist — per-test SessionState-запись дорога, прогресс best-effort). TestRunCallbacks
считает по TestCaseCount/TestStarted/TestFinished (листья), пишет по live-маркеру. GetJobTool отдаёт progress
для running. Ловушка правки: случайно создал лишний RunFinishedUnused-метод — удалил. 94/94 (+2). E2e:
поллинг во время прогона ловит 15 точек прогресса с currentTest. Реализация F3/AC3.5 — raw не менялся.
