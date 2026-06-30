# TASK — m4-idle-keepalive (M4/T4)

## Цель
Закрыть recovery-gap: в фоне (окно не в фокусе + простой) Unity троттлит `EditorApplication.update` →
заклинивает И main-thread-инструменты (Drain), И control-flag (WatchdogTick). `ping` детектит, но не будит.
Дать **opt-in** keepalive (F4/AC4.10).

## Привязка
- Фича/AC: **F4 / AC4.10** (новый — forward-поток raw→wiki→code, интент делегирован пользователем «доделай всё»).
- Системы: `lifecycle-and-reload.md` (§idle-keepalive). Инварианты: INV-2 (чистый C#, opt-in, default-off),
  INV-5 (усиливает self-recovery).

## Подход (research-workflow → дизайн Option B)
Фоновый research-workflow (codebase tick-path + Unity prior-art) дал заземлённый дизайн:
- Корень: ровно ДВА подписчика `update` (Drain + WatchdogTick) → оба троттлятся.
- `No Throttling` (idle=0/mode=1) снимает foreground-кап; против ФОНОВОГО троттла — **версионно-зависимо**
  (патч 2022.3.54f1+/6000.x), не гарантируется. `QueuePlayerLoopUpdate`/`delayCall` НЕ thread-safe;
  `SignalTick` internal/undocumented → отвергнут (хрупко).
- **Option B:** opt-in тогл (default OFF), держит No-Throttling пока сервер включён, переиспользуя
  prefs/`ForceApply` `TestRunnerNoThrottle`. Best-effort + честная оговорка; `ping` — источник истины.

## Реализация
- `ShtlMcpConfig.IdleKeepAlive` (EditorPrefs, machine-local, default false).
- `IdleKeepAlive.Reconcile(wanted)` (Tools, config-агностичен): во время прогона не вмешивается (владеет
  `TestRunnerNoThrottle`); иначе wanted→full-rate, !wanted→Default. Идемпотентно, дёшево.
- Проводка: `ShtlMcpServer.WatchdogTick` (вверху, до enabled-гейта) + `EnsureStarted` + публичный
  `SyncKeepAlive` (дашборд). `TestRunnerNoThrottle`: value-консты + `ForceApply` → internal (переиспользование).
- Дашборд: тогл в settings + тултип (зачем/компромисс/best-effort-оговорка).

## Acceptance
- Reconcile вне прогона приводит prefs к желаемому; во время прогона — no-op; переустанавливается после
  per-run Restore (RED-gate). Default OFF. DAG цел (Tools не зависит от Lifecycle — `wanted` инжектится).
- Регресс зелёный; unit-тесты `IdleKeepAliveTests` (5).

## Статус
✅ Done — Option B реализован, 131/131. Фоновая эффективность — best-effort/версионно-зависима (оговорка
в AC4.10/тултипе/wiki); `ping` (AC4.8) остаётся источником истины. bg-thread-«будилка» отложена как эскалация.
