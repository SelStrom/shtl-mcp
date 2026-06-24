# TASK: T1 — async-job foundation (JobStore + get_job)

**Status:** done (компиляция + get_job headless ✓ автономно; JobStoreTests + GetJobToolTests зелёные в Test Runner)
**Привязка:** F3/AC3.5 (долгие/reload-команды → jobId, результат через get_job), F4/AC4.2 (job
переживает reload), domain «Job» (overview.md), INV-1. Системы: `wiki/systems/lifecycle-and-reload.md` §2,
`command-set.md` (get_job). **Реализация существующего интента → raw/wiki не меняем** (если не всплывёт
отклонение). Первый таск вехи M2 (`wiki/m2-plan.md`).

## Цель

Фундамент async-job: единица работы с `jobId`, статусом и результатом, **переживающая domain reload**
(состояние в `SessionState`), и инструмент `get_job` для опроса. Разблокирует reload-инструменты
(`set_play_mode`/`recompile`-as-job — T3) и `run_tests` (T7 → self-test).

## Scope / не в scope

- **В scope:** `Job` (модель), `JobStore` (in-memory + персист в SessionState, round-trip через reload),
  `get_job` (опрос; unknown-id → понятная ошибка), регистрация в сервере, тесты.
- **Не в scope:** ⏳-runner-хелпер (create job + enqueue work + return jobId) и **resumption работы
  после reload** — у первого реального потребителя (T3 `set_play_mode`/`recompile`-as-job). T1 даёт
  персистентность СОСТОЯНИЯ job; продолжение РАБОТЫ сквозь reload — per-tool (T3).

## Дизайн

- **`Shtl.Mcp.Jobs.Job`** — POCO: `Id`, `Tool`, `Status` (running|done|failed), `Result` (JSON-строка),
  `Error`, `StartedAtTicks`. Сериализуем Newtonsoft.
- **`Shtl.Mcp.Jobs.JobStore`** — in-memory `Dictionary<string,Job>` под локом + персист в
  `SessionState["Shtl.Mcp.Jobs"]` (JSON). Конструктор `Load()` из SessionState (переживает reload).
  Мутации (`Create`/`Complete`/`Fail`) — **только главный поток** (трогают SessionState). `Get` —
  потокобезопасен, читает in-memory (без Unity API) → можно с HTTP-потока (опрос не зависит от main-thread).
- **`Shtl.Mcp.Tools.GetJobTool`** — `get_job(jobId)`; `NeedsMainThread=false` (читает JobStore).
  unknown/empty id → `{error}`; известный → `{jobId, tool, status, result?/error?}`.
- Проводка: `ShtlMcpServer` — поле `JobStore` (как `LogBuffer`), `_tools.Register(new GetJobTool(_jobs))`.

## Acceptance

- AC-1: `JobStore` round-trip через эмулированный reload — job, созданный в одном экземпляре, виден в
  новом экземпляре (чтение из SessionState). RED без персиста.
- AC-2: `Create`→running, `Complete`→done+result, `Fail`→failed+error, `Get(unknown)`→null.
- AC-3: `get_job` unknown-id → понятная ошибка (не исключение); known → корректные поля.
- AC-4: компилируется (self-recompile чисто); тесты зелёные в Test Runner.

## Шаги

1. Job, JobStore, GetJobTool + проводка в ShtlMcpServer.
2. Self-recompile (MCP) → get_logs(error) чисто.
3. Тесты: JobStoreTests (round-trip/состояния), GetJobToolTests (unknown/known/missing-arg).
4. Self-recompile → тесты компилируются; прогон в Test Runner (за пользователем, до появления run_tests=T7).
5. Финал: index/log, статус → done.

## Заметка по INV-3

`get_job` (как и `get_logs`) пока не инжектит `projectName` в ответ. Сквозная инъекция идентичности
(INV-3) во ВСЕ ответы — отдельная cross-cutting задача M2 (сейчас только `status` несёт identity).
Не блокирует T1; зафиксировать как долг.
