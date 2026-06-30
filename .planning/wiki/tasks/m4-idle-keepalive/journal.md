# Journal — m4-idle-keepalive (M4/T4, append-only)

## [2026-07-01] research-workflow → forward-поток → реализация (AC4.10)

**Research (фоновый workflow, 3 агента: codebase tick-path + Unity prior-art web + синтез).** Заземлило:
- Корень gap'а (по коду): ровно ДВА подписчика `EditorApplication.update` — `MainThreadDispatcher.Drain`
  (main-thread-инструменты) и `ShtlMcpBootstrap.Tick→WatchdogTick` (control-flag `.cmd`, heartbeat, re-spawn).
  Глубокий idle троттлит `update` → заклинивает оба; `.cmd`-рестарт (AC2.6) бесполезен (сам на мёртвом тике);
  `ping` (bg-поток) детектит (`mainThreadAgeSeconds` растёт), но не будит.
- Unity prior-art (с источниками): `No Throttling` (`ApplicationIdleTime=0`/`InteractionMode=1`) снимает
  foreground idle-cap; против ФОНОВОГО троттла эффективность **версионно-зависима** (на патченных LTS
  2022.3.54f1+/6000.x фон может троттлиться даже под No-Throttling) — не гарантируется.
  `QueuePlayerLoopUpdate`/`delayCall` НЕ thread-safe; единственный thread-safe «тик-now» —
  internal/undocumented `EditorApplication.SignalTick()` (via reflection) → отвергнут как хрупкий
  (CLAUDE.md §6: не строить на догадках/неподдерживаемом API). App Nap на macOS вероятно уже не при чём
  (редактор держит power-assertion). Prior-art MCP-серверы фоновый троттл не лечат (полагаются на тик/юзера).

**Forward-поток (изменение поведения — новый AC):**
- raw: **AC4.10** под F4 (idle-keepalive, opt-in, best-effort + честная оговорка). Консистентность: INV-2 ок
  (чистый C#, default-off), INV-5 усиливает, AC4.8 комплементарен — конфликтов нет.
- wiki: `lifecycle-and-reload.md` §idle-keepalive (после bg-liveness: детект→митигация).
- code (Option B):
  - `ShtlMcpConfig.IdleKeepAlive` (EditorPrefs machine-local, default false).
  - `IdleKeepAlive.Reconcile(wanted)` (Tools, config-агностичен — DAG цел: Tools не зависит от Lifecycle,
    `wanted` инжектит Lifecycle). Во время прогона (маркер `RunTestsTool.JobMarkerKey`) — no-op (no-throttle
    держит `TestRunnerNoThrottle`); иначе bidirectional: wanted→full-rate, !wanted→Default. Переустанавливается
    после per-run Restore. Переиспользует prefs/`ForceApply` `TestRunnerNoThrottle` (сделал value-консты +
    `ForceApply` internal — единственная точка рефлексии, не дублировать).
  - Проводка `ShtlMcpServer`: `Reconcile(Enabled && IdleKeepAlive)` в `WatchdogTick` (вверху, до enabled-гейта
    → при выключенном сервере троттлинг возвращается к Default) + `EnsureStarted` (немедленно на старте/после
    reload) + публичный `SyncKeepAlive` (дашборд после смены тогла).
  - Дашборд: тогл в settings-foldout + тултип (зачем / компромисс idle-CPU-батарея / best-effort-оговорка).

**Тесты:** `IdleKeepAliveTests` (5: wanted-from-default→no-throttle; !wanted-from-no-throttle→default; во время
прогона не трогает; wanted идемпотентен; **RED-gate**: keepalive переустанавливается после `TestRunnerNoThrottle`
Apply→Restore, вернувшего Default). Мутируют РЕАЛЬНЫЕ throttle-prefs → snapshot/restore в OneTime (как
no-throttle-тесты), `ClearBackup` в RED-gate для детерминизма. **131/131 EditMode зелёные**, компиляция чистая,
сьют не завис.

**Ограничение (задокументировано):** фактическое подавление ФОНОВОГО троттла headless не верифицируется (нужен
расфокус окна) и версионно-зависимо. unit-тесты покрывают pref-логику; эффективность — best-effort, `ping`
(AC4.8) остаётся источником истины. bg-thread-`SignalTick`-«будилка» — отложенная эскалация, если тогл окажется
недостаточен на целевом LTS (тогда — новый raw-AC). **T4 done.**

## [2026-07-01] финальный adversarial-ревью v1 + ремедиация

Workflow (4 измерения × find→adversarially-verify, 14 агентов): **0 BLOCKER, 0 MAJOR, 4 MINOR** (после
скептической верификации). Исправлено 3 из 4 (одно принято как working-as-designed):

- **[MINOR reliability] keepalive-утечка при disabled+reload** — `IdleKeepAlive.Set()` пишет No-Throttling БЕЗ
  бэкапа, а disabled-ветка `ShtlMcpBootstrap.Init` не подписывает Tick → `Reconcile(false)` не вызывается →
  редактор застрял бы в No-Throttling на сессию (нарушение моего же инварианта «выключен → Default»). **Фикс:**
  `IdleKeepAlive.Reconcile(false)` в disabled-ветке Init + дашборд-тогл «Server enabled» зовёт `SyncKeepAlive`
  (немедленный revert при выключении в живой сессии). Логику покрывает `Reconcile_NotWanted…`-тест.
- **[MINOR completeness] `get_config` без `idleKeepAlive`** — агент, диагностирующий фон-затык, не видел
  состояние единственной превентивной настройки. **Фикс:** `["idleKeepAlive"]` в `ConfigSnapshot` (паритет).
- **[MINOR completeness] 1-tick задержка re-assert после SweepOrphan** — best-effort ок, но **фикс:** второй
  `Reconcile` в конце `WatchdogTick` (после SweepOrphan) → переустановка в тот же тик.
- **[MINOR correctness] get_job→✗ в call-tail при упавшем ЗАПРОШЕННОМ job** — **принято как working-as-design**
  (AC5.5: ✗ = `{error}` в результате; get_job легитимно возвращает error упавшего job; вне scope call-tail,
  риск ломать контракт result["error"]). Задокументировано как известное.

Верификация: компиляция чистая, `get_config` отдаёт `idleKeepAlive`, **131/131 EditMode**. v1 подтверждён.
