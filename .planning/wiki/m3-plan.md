# M3 — план вехи: discoverability + надёжность + UI-доводка

> Декомпозиция M3 на bite-sized таски (как `m2-plan.md`). Каждый таск при исполнении получает папку
> `wiki/tasks/<slug>/` (TASK.md + journal.md). Источники: `raw/features/F2,F4,F7`, `raw/domain/overview.md`
> (INV-1..5), `wiki/systems/{lifecycle-and-reload,recovery-discoverability,dashboard,command-set}.md`,
> долги M2 (`m2-plan.md` хвост).

## Объём M3

- **Modal-free scene ops + bg-liveness** (новое, по итогу инцидента M2 — блокирующий «Save Scene?» вешал
  MCP). Реализуется через forward-поток (новые AC).
- **INV-3 identity-инъекция** — `projectName` во ВСЕ ответы тулов (сейчас только `status`).
- **F7 discoverability (полная)** — `recoveryHint` в ответах, durable recovery-блок в `registry.json`,
  opt-in host-крошка. Реализует raw F7.
- **UI-доводка дашборда** — config UI (port range/heartbeat/footgun-тогл AllowRunCsharp), Reload-Domain
  рекомендация-кнопка (AC4.4), per-project config (ProjectSettings provider).
- **PlayMode** — `DisableDomainReload` + двухслойный бэкап `enterPlayModeOptions` для PlayMode-прогонов.
- **Прогресс-стриминг** run_tests-job (completed/total, текущий тест) — опц.

**Не в M3:** v2-инструменты (профайлер, packages CRUD, SSE-стрим прогресса) → M4.

## Почему «modal-free» — первым (инцидент M2)

Блокирующий модал (`EditorUtility.DisplayDialog` / save-scene-промпт) крутит вложенный modal-loop на
главном потоке → `EditorApplication.update` не тикает → MCP main-thread-тулы виснут (листенер на фоне жив).
**Реактивно закрыть модал через MCP нельзя** — канал убит ровно этим модалом. Поэтому: предотвращать модал
+ давать LLM решать программно. Делаем рано — иначе тот же модал может зависнуть прогон тестов/play-mode
во время самой M3-работы.

## Таски и зависимости

| # | Таск (slug) | Содержание | Forward-поток? | Зависит |
|---|---|---|---|---|
| **T1** | `m3-modal-free-scene` | (а) **bg-liveness**: фоново-обслуживаемый сигнал «возраст последнего дренажа main-потока» (отличить «модал/тяжёлая операция» от «сервер мёртв»); (б) **dirtyScene-политика** (`discard`(деф)/`save`/`abort`) на рушащих сцену тулах (`open_scene`, `set_play_mode`, и аудит остальных) — обрабатывать программно, НЕ звать промптящие API; (в) **`sceneDirty`** наружу (read) для проактивного решения LLM. | **ДА** (новые AC под F4/F3 — надёжность + observability) | — |
| **T2** | `m3-identity-injection` | INV-3: `projectName` (идентичность инстанса) во ВСЕ ответы тулов. Обёртка в транспорте/инвокере; для image-`_content` — в text-элемент. | Нет (реализация INV-3, raw есть) | — |
| **T3** | `m3-f7-discoverability` | `recoveryHint` в ответах тулов (расширить `status.recovery`); durable самоописываемый recovery-блок в `registry.json`; `initialize.instructions` усилить; opt-in host-крошка с согласия. | Реализация raw F7 (есть); отклонения → wiki | — |
| **T4** | `m3-dashboard-ui` | UI Toolkit дашборд: config UI (port range/heartbeat/footgun-тогл), Reload-Domain рекомендация-кнопка (AC4.4), per-project config (ProjectSettings provider). | Реализация F2/F4 (есть); UI-детали свободно | T2,T3 (показывает identity/recovery) |
| **T5** | `m3-playmode-reload` | `set_play_mode play` с `DisableDomainReload` + двухслойный бэкап `enterPlayModeOptions` (как CoplayDev); PlayMode-прогон run_tests стабилен. | Реализация F4 (есть) | T1 |
| **T6** | `m3-progress-stream` | Прогресс в run_tests-job: completed/total + текущий тест (через `TestFinished`-колбэк, throttled-персист). Опц. | Нет | — |

## Порядок (волны)

1. **Надёжность-де-риск:** **T1** (modal-free + bg-liveness) — раньше всего, защищает остаток M3.
2. **Cross-cutting:** **T2** (identity-инъекция — дёшево, трогает все тулы).
3. **Discoverability/UI:** **T3** (F7) → **T4** (дашборд, показывает T2/T3).
4. **Добивка:** **T5** (PlayMode), **T6** (прогресс-стриминг).

**Критический путь:** T1 → (T4, T5).

## Прогресс (2026-06-25)

- ✅ **T1 `m3-modal-free-scene`** — bg-liveness (`ping`, NeedsMainThread=false; отличает «главный поток
  завис» от «down») + `sceneDirty`/`scenePath` в `get_hierarchy`. **T1b dirtyScene-политика СНЯТА** —
  аудит: наши scene-тулы зовут непромптящие API (`OpenScene`/`EnterPlaymode`/`Refresh` не открывают модал);
  инцидентный модал был не от них. 85/85. Forward-поток raw F4/AC4.8-4.9 + wiki + code.
- ✅ **T2 `m3-identity-injection`** — INV-3: `projectName` во все ответы тулов (text → поле JSON, image →
  text-элемент, ошибка → префикс), кросс-каттинг в `McpRouter`. 87/87.
- ✅ **T3 `m3-f7-discoverability`** — durable recovery-блок в registry.json (AC7.1), усиленный
  initialize.instructions (AC7.2), `recoveryHint` в ответах (AC7.3). AC7.4 host-крошка → отложена (opt-in
  + дашборд T4). 88/88.
- ✅ **T4 `m3-dashboard-ui`** — config UI (Enabled/AllowRunCsharp-footgun/port range/heartbeat) +
  Reload-Domain рекомендация-кнопка (AC4.4). Компиляция чистая, 88/88; визуальный осмотр — за человеком
  (Window/Shtl MCP). Per-project ProjectSettings + AC7.4 host-крошка — отложены.
- ✅ **T5 `m3-playmode-reload`** — `PlayModeOptionsGuard` (двухслойный бэкап enterPlayModeOptions, форсит
  DisableDomainReload для PlayMode-прогона) + хук в run_tests. 92/92. PlayMode-прогон e2e отложен (нет
  PlayMode-тестов).
- ✅ **T6 `m3-progress-stream`** — live-прогресс в run_tests-job (`get_job` → `{completed,total,
  currentTest}`); in-memory best-effort. 94/94.

**🎉 M3 ЗАВЕРШЁН.** T1–T6 done. 35 тулов (+`ping`), 94 EditMode-теста зелёные. modal-free+bg-liveness,
INV-3 identity, F7 discoverability (durable recovery в registry), дашборд UI, PlayMode DisableDomainReload,
прогресс-стриминг. Долги → M4: per-project config + AC7.4 host-крошка, PlayMode-прогон e2e (нет PlayMode-
тестов), v2-тулы (профайлер/packages CRUD/SSE).

## Точки контроля

- T1 меняет НАМЕРЕНИЕ (новые AC надёжности) → forward-поток raw→wiki→code, атомарно. Интент уже задан
  человеком (запрос: «LLM решает про сохранение, обычно не сохранять, без блокирующего модала»).
- Остальные таски — реализация уже зафиксированного raw (F2/F4/F7/INV-3); wiki обновляется при отклонениях.
- Не нарушать INV-1..5; тесты по факту реализации (TDD для сложных багфиксов/рефакторинга).
