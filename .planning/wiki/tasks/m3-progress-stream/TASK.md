# TASK: T6 — progress-streaming run_tests

**Status:** done (live-прогресс в get_job; 94/94)
**Привязка:** F3/AC3.5 (доводка job-модели). Реализация — raw не менялся.

## Реализация

- `Job`: поля `Completed`/`Total`/`CurrentTest` (best-effort, in-memory, НЕ персистятся — транзиентно).
- `JobStore.SetProgress(id, completed, total, current)` — обновляет running-job in-memory без персиста
  (per-test SessionState-запись была бы дорогой; прогресс — лучшее-усилие, переживать reload не обязан).
- `TestRunCallbacks`: `RunStarted` → total = `testsToRun.TestCaseCount`, completed=0; `TestStarted` (лист)
  → current; `TestFinished` (лист) → completed++; `PushProgress` пишет в job по live-маркеру.
- `GetJobTool`: для `running`-job с total>0 добавляет `progress {completed,total,currentTest}`.

## Верификация

- Unit (`GetJobToolTests`, +2): running-job отдаёт progress; на done — не отдаёт. **94/94** (92 + 2).
- E2e: поллинг get_job во время прогона — 15 уникальных точек прогресса (`0/94` → `6/94
  (RunCsharp_Enabled_EvaluatesExpression)` → …), currentTest показывает текущий тест.

## Заметки
- После reattach (reload в середине) счётчик с нуля (in-memory) — прогресс может недосчитать; терпимо.
