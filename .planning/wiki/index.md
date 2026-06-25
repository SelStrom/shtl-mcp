# Wiki — каталог

Сначала читай этот индекс, потом проваливайся в страницы. Обновляется при каждом
изменении wiki.

## systems/ — архитектура (intent-derived, gate между намерением и кодом)
- [architecture](systems/architecture.md) — общая схема: in-Unity HTTP MCP,
  модель потоков, стек, разбиение на asmdef, стратегия тестов.
- [multi-instance](systems/multi-instance.md) — порт по пути, реестр, дедуп
  serverName, `claude mcp add`, префикс инструментов, клоны/worktrees, scope.
- [lifecycle-and-reload](systems/lifecycle-and-reload.md) — выживание при domain
  reload, async-job, watchdog, главный поток, Reload-Domain рекомендация.
- [command-set](systems/command-set.md) — полный набор инструментов (тонкое ядро
  + escape hatches), async-команды, контракт, v2.
- [recovery-discoverability](systems/recovery-discoverability.md) — как
  использующая модель узнаёт о восстановлении (durable-реестр, пре-брифинг,
  recoveryHint, opt-in host-крошка).
- [dashboard](systems/dashboard.md) — UI Toolkit окно, макет, элементы.

## concepts/ — (пока пусто; intent-derived понятия по мере надобности)

## code/ — reference-страницы по коду (pin к коммиту)
- [m1-server](code/m1-server.md) — карта кода M1: модули, namespace'ы, публичные
  контракты, подключение, тесты (pin `799bf46`).

## explorations/ — (пока пусто; синтез из query)

## tasks/ — история тасков
- [README](tasks/README.md) — конвенция ведения истории тасков.
- [m1-walking-skeleton](tasks/m1-walking-skeleton/TASK.md) — вертикальный срез:
  connect + status/get_logs + выживание при reload. План — `PLAN.md` (13 задач, TDD).
- [m1-reload-survival-test](tasks/m1-reload-survival-test/TASK.md) — автотест reload-survival
  (RED-gate): EditMode `[UnityTest]` через `WaitForDomainReload` + watchdog re-bind; `status`
  round-trip как проба. Закрывает интерактивную приёмку reload из m1-walking-skeleton.
- [status-reload-observability](tasks/status-reload-observability/TASK.md) — наблюдаемость re-spawn
  в `status`: `reloadCount` (durable) + `listenerUptimeSeconds` (сброс при re-spawn). Forward-поток
  F4/AC4.7 (raw+wiki+code).
- [reload-respawn-focus-independent](tasks/reload-respawn-focus-independent/TASK.md) — bugfix INV-5:
  re-spawn после reload через afterAssemblyReload в InitializeOnLoad (focus-independent). Плюс MCP
  `recompile`-инструмент → автономная дев-петля (edit → recompile → self-recovery).
- [m2-async-job](tasks/m2-async-job/TASK.md) — T1: JobStore (переживает reload) + `get_job`.
  Фундамент async-job для reload-инструментов и run_tests.
- [m2-run-tests](tasks/m2-run-tests/TASK.md) — T7: `run_tests` (job+polling) + no-throttle
  (`TestRunnerNoThrottle`, двухслойный бэкап) + orphan-таймаут + reload-spanning (изоляция ключа JobStore).
  Self-test полностью рабочий (полный сьют headless, переживает reload, сервер отзывчив).
- [m2-asmdef-split](tasks/m2-asmdef-split/TASK.md) — T2: единый Editor-asmdef → 6 сборок
  (Transport/Dispatcher/Registry/Tools/Lifecycle/UI), разрыв цикла Lifecycle↔Tools (DI JobStore +
  Logging→Dispatcher). Характеризация 52/52 + reload-spanning зелёные.
- [m2-play-recompile](tasks/m2-play-recompile/TASK.md) — T3: `recompile` (job) + `set_play_mode` через
  обобщённый `ReloadJobs` (durable-маркер + финализация после reload). E2E: результат job доставлен
  после самотриггернутого reload. 59/59.
- [m2-assets](tasks/m2-assets/TASK.md) — T4: `clear_logs`, `refresh_assets` (job), find/read/move/delete
  ассетов + create_folder (AssetDatabase CRUD). 63/63 + e2e round-trip.
- [m2-prefabs](tasks/m2-prefabs/TASK.md) — T5: create/open/save/close/instantiate prefab (PrefabUtility +
  prefab-stage). 66/66 + round-trip на реальном PrefabUtility.
- [m2-scene-objects](tasks/m2-scene-objects/TASK.md) — T6: иерархия/GameObjects (create/modify/destroy/
  set_parent/find/hierarchy) + scene/selection/SerializedObject (get/modify_object, open/save_scene,
  get/set_selection). 72/72.
- [m2-screenshot](tasks/m2-screenshot/TASK.md) — T9: `screenshot(view=game|scene)` как MCP image-content;
  router-конвенция `_content`. 73/73.
- [m2-config](tasks/m2-config/TASK.md) — T11: бэкенд конфига (EditorPrefs: port range, heartbeat, footgun
  AllowRunCsharp), `get_config`. 76/76.
- [m2-escape-hatches](tasks/m2-escape-hatches/TASK.md) — T8: `execute_menu_item`, `run_csharp` (footgun,
  gated; CodeDom-компиляция). 79/79.
- [m2-control-flag](tasks/m2-control-flag/TASK.md) — T10: watchdog исполняет `.cmd`-флаг (`restart`,
  AC2.6) + `status.recovery` (AC2.7). E2E.
- [m3-modal-free-scene](tasks/m3-modal-free-scene/TASK.md) — M3/T1: bg-liveness (`ping`, AC4.8) +
  `sceneDirty` (AC4.9); dirtyScene-политика снята (аудит: scene-тулы используют непромптящие API).
- [m3-identity-injection](tasks/m3-identity-injection/TASK.md) — M3/T2: INV-3 `projectName` во все ответы
  тулов (кросс-каттинг в McpRouter).
- [m3-f7-discoverability](tasks/m3-f7-discoverability/TASK.md) — M3/T3: durable recovery-блок в
  registry.json (AC7.1), усиленный initialize.instructions (AC7.2), `recoveryHint` (AC7.3).
- [m3-dashboard-ui](tasks/m3-dashboard-ui/TASK.md) — M3/T4: config UI + Reload-Domain кнопка (AC4.4).
- [m3-playmode-reload](tasks/m3-playmode-reload/TASK.md) — M3/T5: `PlayModeOptionsGuard` (DisableDomainReload
  для PlayMode-прогона, двухслойный бэкап).
- [m3-progress-stream](tasks/m3-progress-stream/TASK.md) — M3/T6: live-прогресс run_tests в `get_job`.

## Планы вех
- [m2-plan](m2-plan.md) — декомпозиция M2 (полный тулсет + async-job + control-flag + config +
  asmdef split) на bite-sized таски T1–T11 с зависимостями и порядком волн. **✅ завершён.**
- [m3-plan](m3-plan.md) — декомпозиция M3 (modal-free+bg-liveness, INV-3 identity, F7 discoverability,
  дашборд UI, PlayMode, прогресс). 🔄 T1a bg-liveness ✅.

## Карта намерения (raw/)
- `raw/domain/overview.md` — сущности, инварианты INV-1..5, ограничения.
- `raw/features/F1..F6` — фичи с acceptance criteria.
- `raw/epochs.md` — эпохи.
