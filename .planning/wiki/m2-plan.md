# M2 — план вехи: полный тулсет + async-job + control-flag

> Декомпозиция M2 на bite-sized таски. Каждый таск при исполнении получает папку
> `wiki/tasks/<slug>/` (TASK.md + journal.md). Этот документ — индекс и порядок,
> не подменяет per-task артефакты. Источники: `raw/features/F2,F3,F4`,
> `wiki/systems/{architecture,command-set,lifecycle-and-reload}.md`, `raw/domain/overview.md`.

## Объём M2

- **Полный Core-тулсет** (~18 инструментов сверх `status`/`get_logs`) — F3/AC3.1.
- **Async-job модель** (`jobId` + `get_job`, JobStore в `SessionState`, переживает reload) —
  F3/AC3.5, F4/AC4.2, INV-1.
- **Control-flag рестарт** (`~/.unity-mcp/<serverName>.cmd`, исполняемый watchdog'ом) — F2/AC2.6–2.7.
- **Config** (port range, auto-start, heartbeat, footgun-флаг `run_csharp`) — F2/AC2.1 (backend; UI-доводка → M3).
- **6-asmdef split** — architecture.md (сейчас один Editor-asmdef папками).

**Не в M2:** F7 discoverability (recoveryHint/durable recovery-блок/host-крошка), доводка дашборда,
Reload-Domain рекомендация-кнопка (AC4.4), v2-инструменты (профайлер, packages CRUD, SSE-стрим) → **M3**.

## Сквозной контракт (в acceptance КАЖДОГО tool-таска, не отдельные таски)

- JSON-схема параметров + человекочитаемое описание в `tools/list` (AC3.4).
- Ответ несёт идентичность инстанса `projectName` (INV-3).
- Play/Edit-корректность (AC4.5): осмысленные в Play работают в Play; Editor-only при неверном
  режиме → понятная ошибка в ответе, не краш.
- Reload-триггерящие инструменты — только как async-job (INV-1): синхронный ответ умрёт с доменом.
- Тесты по факту реализации; поведение, а не приватное состояние; реальное вместо моков, где дёшево.

## Таски и зависимости

| # | Таск (slug) | Содержание | Зависит от |
|---|---|---|---|
| **T1** | `m2-async-job` | JobStore (SessionState round-trip, переживает reload), модель Job (id/status/result/error), `get_job`, ⏳-контракт-хелпер для инструментов. Тесты: JobStore round-trip через эмулированный reload, `get_job` unknown-id → ошибка. | — |
| **T2** | `m2-asmdef-split` | Разбить единый `Shtl.Mcp.Editor` на 6 сборок (Transport/Dispatcher/Registry/Tools/Lifecycle/UI). Характеризационно: текущие 38 тестов + headless smoke зелёные. | — (но до разрастания тулов) |
| **T3** | `m2-play-recompile` | `set_play_mode` ⏳, `recompile` ⏳ (job переживает reload, который сам же триггерит). RED-gate: результат job доставлен после reload. | T1 |
| **T4** | `m2-assets` | `clear_logs`; `refresh_assets` ⏳; `find_assets`, `read_asset`, `move_asset`, `delete_asset`, `create_folder` (AssetDatabase CRUD). | T1 (для refresh_assets ⏳) |
| **T5** | `m2-prefabs` | `create_prefab`, `open_prefab`/`save_prefab`/`close_prefab` (prefab-stage), `instantiate_prefab`. | T2 (размещение) |
| **T6** | `m2-scene-objects` | `get_hierarchy`, `find_gameobject`, `gameobject_create/modify/destroy`, `set_parent`, `get_object`/`modify_object` (через `SerializedObject`), `open_scene`/`save_scene`, `get_selection`/`set_selection`. Крупный — при исполнении разбить на T6a (hierarchy/gameobjects) + T6b (scene/selection/SerializedObject). | T2 |
| **T7** | `m2-run-tests` | `run_tests` ⏳ (EditMode/PlayMode → результаты как job). | T1 |
| **T8** | `m2-escape-hatches` | `execute_menu_item`; `run_csharp` (компиляция+исполнение произвольного Editor-C#, результат/ошибки; gated footgun-флагом). | T11 (флаг), T1 (если长 → job) |
| **T9** | `m2-screenshot` | `screenshot(view=game\|scene)` — кадр как image-content. | T2 |
| **T10** | `m2-control-flag` | `.cmd`-канал: watchdog атомарно читает+исполняет `restart` (AC2.6); recovery-playbook в подсказках ошибок инструментов и описании `status` (AC2.7). | watchdog (есть) |
| **T11** | `m2-config` | Config-asset (ProjectSettings/EditorPrefs): port range, auto-start, heartbeat-интервал, footgun-флаг `run_csharp` (AC2.1). UI-доводка → M3. | — |

## Предусловие автономии (done) — reload-respawn-focus-independent

Перед self-service дев-петлёй исправлен баг lifecycle: re-spawn после reload зависел от
`EditorApplication.update` (троттлится в фоне) → MCP-recompile вешал сервер без фокуса. Фикс
(подписка `afterAssemblyReload` в `[InitializeOnLoad]`-ctor) сделал re-spawn focus-independent —
теперь MCP-`recompile` self-recover'ится в фоне (проверено). Это предусловие для T3/T7/T10 (любая
MCP-инициированная reload-операция). `recompile`-инструмент добавлен и работает автономно.
**Следствие:** self-test (run_tests=T7) требует T1 → приоритет T1→T7 для полной автономии.

## Прогресс (2026-06-24)

- ✅ **Предусловие автономии** (`reload-respawn-focus-independent`) + MCP-инструмент **`recompile`** →
  автономная дев-петля: edit → MCP `recompile` → self-recovery в фоне → headless curl-проверка.
- ✅ **T1 `m2-async-job`** — JobStore (переживает reload) + `get_job`; тесты зелёные.
- ✅ **T7 `m2-run-tests`** — `run_tests` (job+polling) + **no-throttle** (`TestRunnerNoThrottle`,
  двухслойный бэкап) + **orphan-таймаут** + **reload-spanning** (фикс изоляции ключа JobStore). Полный
  сьют headless **52/52 passed**, переживает форсированный reload (17→18), сервер отзывчив всё время
  (0.05–0.18с). **Self-test полностью рабочий.** См. `tasks/m2-run-tests/TASK.md` (AC-1..4 ✅).
- ✅ **T2 `m2-asmdef-split`** — единый `Shtl.Mcp.Editor` разбит на 6 Editor-сборок (Transport/Dispatcher/
  Registry/Tools/Lifecycle/UI). Цикл Lifecycle↔Tools разорван (DI `JobStore` в `TestRunCallbacks` +
  Logging→Dispatcher). Характеризация: 52/52 + reload-spanning против реального split зелёные.
- ✅ **T3 `m2-play-recompile`** — `recompile` (переписан на job) + `set_play_mode` как async-job через
  обобщённый `ReloadJobs` (durable-маркер + финализация после reload). E2E: recompile job доставлен
  после самотриггернутого reload; set_play_mode play/edit. 59/59 зелёные.
- ✅ **T4 `m2-assets`** — `clear_logs`, `refresh_assets` (job), `find_assets`, `read_asset`, `move_asset`,
  `delete_asset`, `create_folder` (AssetDatabase CRUD). 63/63 + e2e (round-trip на реальном AssetDatabase).
- ✅ **T5 `m2-prefabs`** — `create_prefab`, `open_prefab`/`save_prefab`/`close_prefab` (prefab-stage),
  `instantiate_prefab`. 66/66 + prefab round-trip на реальном PrefabUtility/PrefabStage.
- ✅ **T6 `m2-scene-objects`** (T6a+T6b) — get_hierarchy, find_gameobject, gameobject_create/modify/
  destroy, set_parent, get_object/modify_object (SerializedObject), open_scene/save_scene, get_selection/
  set_selection. 72/72 + round-trip на реальной сцене.
- ✅ **T9 `m2-screenshot`** — `screenshot(view=game|scene)` как MCP image-content (PNG/base64);
  McpRouter-конвенция `_content`. 73/73 + e2e (валидный PNG).
- ✅ **T11 `m2-config`** — бэкенд конфига (EditorPrefs): port range, heartbeat, footgun `AllowRunCsharp`;
  `get_config` (read-only). 76/76.
- ✅ **T8 `m2-escape-hatches`** — `execute_menu_item`; `run_csharp` (footgun, gated; CodeDom-компиляция
  работает — `40+2`→«42»). 79/79.
- ✅ **T10 `m2-control-flag`** — watchdog атомарно исполняет `~/.unity-mcp/<serverName>.cmd` (`restart`,
  AC2.6); `status.recovery` (AC2.7). E2E: флаг потреблён + листенер пересоздан.

**🎉 M2 ЗАВЕРШЁН.** Все T1–T11 done. 34 тула, 79 тестов зелёные. Полный e2e: reload-spanning job'ы,
no-throttle, 6-asmdef split, control-flag. Долги M2 (→ M3): INV-3 identity-инъекция (cross-cutting),
F7 discoverability (recoveryHint/durable-блок/host-крошка), per-project config UI, PlayMode
DisableDomainReload, прогресс-стриминг run_tests.

Зарегистрированные тулы (30): status, get_logs, recompile, set_play_mode, get_job, run_tests, clear_logs,
refresh_assets, find_assets, read_asset, move_asset, delete_asset, create_folder, create_prefab,
open_prefab, save_prefab, close_prefab, instantiate_prefab, get_hierarchy, find_gameobject,
gameobject_create, gameobject_modify, gameobject_destroy, set_parent, get_object, modify_object,
open_scene, save_scene, get_selection, set_selection.

**Долги (не блокеры, см. TASK.md):** INV-3 identity-инъекция в ответы (cross-cutting M2); прогресс-стриминг
в run_tests-job (опц.); PlayMode DisableDomainReload + бэкап enterPlayModeOptions (отдельно).

Зарегистрированные инструменты (server-side `tools/list`): `status, get_logs, recompile, get_job, run_tests`.
**Дев-петля для агента:** MCP `recompile` (self-recovers в фоне) + headless `curl` JSON-RPC на
`127.0.0.1:9730/` для `run_tests`/`get_job` (новые тулы видны server-side без reconnect клиента).

## Порядок (волны)

1. **Фундамент:** **T1** (async-job) → **T2** (asmdef split). T1 разблокирует reload-инструменты;
   T2 дешевле сделать сейчас (кодовая база ещё мала ~23 файла), пока тулы не расплодились.
2. **Тулы (параллельно):** T3, T4, T5, T6, T8, T9. (T3/refresh_assets — после T1; T8 — после T11/флага.)
   Между собой независимы → можно брать в любом порядке/параллельно.
3. **Механизмы/добивка:** T7 (run_tests), T10 (control-flag), T11 (config — можно раньше, если T8 берётся первым).

**Критический путь:** T1 → (T3, T7). Остальное — широкая параллель после T2.

## Точки контроля

- Каждый tool-таск проходит forward-поток только если меняет намерение; добавление инструмента из
  уже зафиксированного F3/AC3.1 — это реализация (raw уже есть), wiki command-set обновляется при
  отклонениях. Контракт инструмента — по сквозному списку выше.
- async-job (T1) и control-flag (T10) — затрагивают INV-1/INV-5; их acceptance включает reload-выживание
  (как `m1-reload-survival-test`).
