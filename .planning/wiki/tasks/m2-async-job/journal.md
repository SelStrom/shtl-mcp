# Journal — m2-async-job (T1, append-only)

## [2026-06-23] реализация ядра + self-verify

Написано: Editor/Jobs/{Job,JobStore}.cs, Editor/Tools/GetJobTool.cs; проводка в ShtlMcpServer
(using Shtl.Mcp.Jobs, поле `JobStore _jobs`, register GetJobTool). JobStore: in-memory dict под локом +
персист в SessionState["Shtl.Mcp.Jobs"] (Newtonsoft), Load() в ctor (переживает reload). Мутации —
главный поток (SessionState); Get — потокобезопасен без Unity API (опрос с HTTP-потока). get_job:
NeedsMainThread=false, unknown/empty id → структурированная ошибка.

Self-verify (автономно, через MCP recompile + headless curl):
- 2 self-recompile (reloadCount →9 →10), get_logs(error) пуст → код и тесты компилируются чисто.
- tools/list server-side: [status, get_logs, recompile, get_job] → get_job зарегистрирован.
- headless get_job: unknown id → "unknown jobId: ...", missing arg → "jobId is required" (AC-3 ✓).

Тесты написаны: JobStoreTests (Create/Complete/Fail/Get-unknown + Survives_Reload_ViaSessionState =AC-1),
GetJobToolTests (unknown/missing/known/NeedsMainThread). Прогон в Test Runner — за пользователем
(AC-1/AC-2 + полный AC-3), до появления run_tests (T7).

Заметка: get_job не инжектит projectName (как get_logs) — INV-3 cross-cutting долг M2.
Отложено в T3: ⏳-runner-хелпер + resumption работы сквозь reload (у первого потребителя).

## [2026-06-24] done

JobStoreTests + GetJobToolTests прогнаны в Test Runner (пользователь): все зелёные. AC-1 (round-trip
через эмулированный reload) и AC-2 подтверждены, AC-3 закрыт (headless + unit). T1 done — фундамент
async-job готов. Дальше: T7 (run_tests) на этом фундаменте.
