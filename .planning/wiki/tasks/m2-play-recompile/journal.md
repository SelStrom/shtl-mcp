# Journal — m2-play-recompile (T3, append-only)

## [2026-06-24] реализация + e2e

Обобщил reload-spanning паттерн T7 на не-тест-команды: `ReloadJobs` (durable-маркер `Shtl.Mcp.ReloadJob`
в SessionState + финализация после reload). Два триггера завершения:
- recompile — по росту `reloadCount` (reload случился = успех); grace 5с на старт компиляции; ошибки
  компиляции из `CompilationPipeline.assemblyCompilationFinished` (durable в SessionState); no-reload+
  no-error+grace → done(no changes).
- set_play_mode — по `EditorApplication.playModeStateChanged` (достижение target); backstop-таймаут 30с.

`RecompileTool` переписан с fire-and-forget на job (теперь get_job даёт явный done/fail с compile-инфо,
а не отдельный поллинг status+get_logs). `SetPlayModeTool` новый. reloadCount проброшен как `Func<int>`
(не Tools→Lifecycle — DAG сборок из T2 цел). Проводка: подписки в EnsureStarted (переживают reload),
FinalizeOnTick в WatchdogTick, OnPlayModeChanged-форвардер в ShtlMcpServer.

Ловушка хука: первый Write теста с однострочным if/else заблокирован brace-style-guard → переписал с {}.

Верификация (headless, автономно):
- Компиляция чистая (reload 25), `set_play_mode` в tools/list. Полный сьют **59/59** (52 + 7 ReloadJobs).
- recompile force:true → jobId → get_job `done {reloaded:true, status:recompiled}` ПОСЛЕ reload (1.6с) —
  RED-gate (результат доставлен после самотриггернутого reload).
- set_play_mode play → `done {mode:play}` (1.1с), edit → `done {mode:edit}` (0.5с); финальный mode=edit,
  health=ok (редактор не оставлен в play).

Реализация интента F3/AC3.1, F4/AC4.2, INV-1 (lifecycle-and-reload §2 уже перечисляет эти тулы как
async-job) — raw не менялся.
