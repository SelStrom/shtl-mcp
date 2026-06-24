# TASK: T3 — set_play_mode + recompile как async-job

**Status:** done (оба reload-job доставляют результат после reload; 59/59 + e2e зелёные)
**Привязка:** F3/AC3.1 (set_play_mode), F4/AC4.2 (job переживает reload), INV-1 (reload-команды — только
async-job). Системы: `lifecycle-and-reload.md` §2. На фундаменте T1 (JobStore). Паттерн обобщён из T7.

## Цель

`recompile` и `set_play_mode` — reload-триггерящие команды → синхронный ответ умер бы вместе с доменом
(INV-1). Сделать их async-job, чьи результаты доставляются ПОСЛЕ reload, который они сами вызывают.

## Реализация

- **`ReloadJobs`** (`Editor/Tools/ReloadJobs.cs`) — обобщённый reload-spanning финализатор. Durable-маркер
  `Shtl.Mcp.ReloadJob` = `{jobId, kind, reloadCount, startedTicks, target}` в SessionState (переживает
  reload). Финализация:
  - **recompile**: `FinalizeOnTick` (с тика watchdog) — `reloadCount` вырос → `done {reloaded:true,
    status:recompiled}`; иначе после grace (5с) без reload → `fail` с ошибками компиляции (собраны через
    `CompilationPipeline.assemblyCompilationFinished` → `OnAssemblyCompiled`, durable) либо `done
    {reloaded:false, no changes}`.
  - **set_play_mode**: `OnPlayModeChanged` (подписка `EditorApplication.playModeStateChanged`) — done по
    достижению целевого режима; backstop-таймаут (30с) в `FinalizeOnTick`.
  - Один reload-job за раз (guard на маркере).
- **`RecompileTool`** — переписан с fire-and-forget на job: `ReloadJobs.Begin` → `AssetDatabase.Refresh`
  (+ `force` → `RequestScriptCompilation`) → возвращает jobId. Результат — через get_job после reload.
- **`SetPlayModeTool`** (новый) — `state: play|edit`. Уже в целевом режиме → мгновенный done-job (без
  перехода). Иначе `Begin` → `EnterPlaymode`/`ExitPlaymode` → jobId.
- **Проводка `ShtlMcpServer`**: регистрация обоих тулов (с `() => ReloadCount`); подписки
  `assemblyCompilationFinished` (ReloadJobs.OnAssemblyCompiled) + `playModeStateChanged` (→
  ReloadJobs.OnPlayModeChanged) переустанавливаются в `EnsureStarted` (переживают reload);
  `ReloadJobs.FinalizeOnTick` в `WatchdogTick`.

reloadCount передаётся в тулы как `Func<int>` (без связки Tools→Lifecycle — не ломает asmdef DAG из T2).

## Верификация

- Unit (`ReloadJobsTests`, 7): Begin-guard, recompile reload→done, до-grace running, после-grace no-changes
  done, после-grace compile-error fail, set_play_mode target-reached done, wrong-state running. ✅
- Полный сьют: **59/59 passed** (52 + 7).
- **E2E recompile-as-job (RED-gate):** `recompile force:true` → jobId → `done {reloaded:true,
  status:recompiled}` доставлен ПОСЛЕ reload (1.6с). ✅
- **E2E set_play_mode:** play → `done {mode:play}` (1.1с), edit → `done {mode:edit}` (0.5с); редактор
  вернулся в edit, health=ok. ✅

## Долги / заметки

- INV-3 identity-инъекция в ответы (общий долг M2) — не сделано.
- `refresh_assets` ⏳ (тяжёлый вариант) — в T4 (использует тот же reload-job паттерн при необходимости).
- Compile-ошибки берутся из `assemblyCompilationFinished`; редкий no-reload+no-error+no-changes →
  done(no changes) после grace.
