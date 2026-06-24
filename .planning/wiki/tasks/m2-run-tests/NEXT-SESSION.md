# Промпт следующей сессии (shtl-mcp) — M2 завершён

> Скопировать как стартовое сообщение следующей сессии (или прочитать как бриф).

---

Контекст: `CLAUDE.md`, `.planning/wiki/m2-plan.md` (секция «Прогресс» — все T1–T11 ✅),
`.planning/wiki/index.md`, `.planning/wiki/log.md` (последняя строка `milestone-complete | M2`). Проверь
соединение: MCP `status`.

## Где мы — M2 ЗАВЕРШЁН 🎉

Полный Core-тулсет (**34 тула**), **79 EditMode-тестов зелёные**, всё верифицировано headless автономной
дев-петлёй. Реализовано:
- **Async-job + reload-spanning** (`ReloadJobs`/`JobStore`): `run_tests`, `recompile`, `set_play_mode`,
  `refresh_assets` переживают reload, который сами триггерят; результат доставляется после reload.
- **No-throttle** (`TestRunnerNoThrottle`, двухслойный бэкап) — отзывчивость сервера в фоне во время прогонов.
- **6-asmdef split** (Transport/Dispatcher/Registry/Tools/Lifecycle/UI; цикл разорван DI + Logging→Dispatcher).
- **Тулсет**: assets (CRUD/clear_logs/refresh), prefabs (create/open/save/close/instantiate), scene-objects
  (hierarchy/gameobject CRUD/set_parent/get-modify_object через SerializedObject/open-save_scene/selection),
  screenshot (image-content), get_config, escape hatches (execute_menu_item, run_csharp — footgun, CodeDom).
- **Control-flag** (`~/.unity-mcp/<serverName>.cmd` restart, AC2.6) + `status.recovery` (AC2.7).

Каждый таск — папка `.planning/wiki/tasks/m2-*/` (TASK.md + journal.md).

## Дев-петля (как работать автономно)

Новые тулы видны server-side сразу после компиляции, но MCP-клиент — после `/mcp`-reconnect (делает
пользователь). Дёргай headless: `curl -s -X POST -d '{"jsonrpc":"2.0","id":1,"method":"tools/call",
"params":{"name":"<tool>","arguments":{...}}}' http://127.0.0.1:9730/`. Цикл: правка → headless
`recompile force:true` → Bash-поллинг `status` (isCompiling→false) → `get_logs(error)` → `run_tests`
полный сьют. Полный сьют через MCP безопасен (тесты на изолированных SessionState-ключах + брекетинг).

## Дальше — M3 (новая веха)

Создать `.planning/wiki/m3-plan.md` (декомпозиция). Долги/скоуп M3 (из raw + TASK.md-долгов):
- **F7 discoverability** (полная): `recoveryHint` во всех ответах, durable recovery-блок в
  `registry.json`, опц. host-крошка с согласия. Сейчас — только `status.recovery` (light).
- **INV-3 identity-инъекция** в ответы всех тулов (`projectName`) — cross-cutting (сейчас только `status`).
- **UI-доводка дашборда** (`dashboard.md`): config UI (port range/heartbeat/footgun-тогл), Reload-Domain
  рекомендация-кнопка (AC4.4), per-project config (ProjectSettings provider).
- **PlayMode**: `DisableDomainReload` + двухслойный бэкап `enterPlayModeOptions` (для PlayMode-прогонов).
- **Прогресс-стриминг** в run_tests-job (completed/total, текущий тест); v2-инструменты (профайлер,
  packages CRUD, SSE-стрим прогресса).

## Инварианты (не нарушать) — `raw/domain/overview.md` INV-1..5; forward-поток raw→wiki→code для
изменений поведения; добавление тулов из уже зафиксированного raw — реализация (wiki обновлять при отклонениях).
