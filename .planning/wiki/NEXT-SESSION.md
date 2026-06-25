# Промпт следующей сессии (shtl-mcp) — старт M3

> Скопировать как стартовое сообщение следующей сессии (или прочитать как бриф).
> Заменяет таск-уровневый `tasks/m2-run-tests/NEXT-SESSION.md`.

---

Продолжаем shtl-mcp. M2 завершён и **смержен в `main`** (`113b82a merge: M2`). Контекст: `CLAUDE.md`,
`.planning/wiki/m2-plan.md` (все T1–T11 ✅ + milestone-complete + долги→M3), `.planning/wiki/index.md`,
`.planning/wiki/log.md` (последние строки `review-fix` + `milestone-complete`).

## ⚠️ ПЕРВЫЙ ШАГ — разбудить Unity (если ещё спит)

В конце прошлой сессии живой Unity-сервер ушёл в **глубокий фоновый троттлинг**: `EditorApplication.update`
перестал тикать (heartbeat в `~/.unity-mcp/registry.json` протух, `status` → drain-timeout, хотя
HTTP-листенер на фоне жив — `tools/list` отвечает). Триггер: файловая чехарда merge'а (checkout→merge→
recompile) + idle при несфокусированном окне. **Control-flag тут бессилен** (ему нужен тикающий main-поток).

**Проверь и при необходимости попроси пользователя сфокусировать окно Unity** (это разбудит update-loop):
```
curl -s --max-time 3 -X POST -d '{"jsonrpc":"2.0","id":1,"method":"tools/call","params":{"name":"status","arguments":{}}}' http://127.0.0.1:9730/
```
Если `status` отвечает (health=ok) — Unity жив, продолжай. Если drain-timeout — `cat ~/.unity-mcp/registry.json`
(свежий ли lastHeartbeat?), `kill -0 <pid>`; если main-тик мёртв → **попроси человека кликнуть на окно Unity**
(`! open -a Unity` не поможет фокусу надёжно; решение проекта — Unity моделью не управляем глубже control-flag).
Когда оживёт — контрольный `run_tests` (assembly `Shtl.Mcp.Editor.Tests`), ожидаем **81/81**.

> **Находка для M3 (recovery-gap):** глубокий idle-троттлинг не будится ни входящим HTTP, ни control-flag
> (оба зависят от тикающего update). Кандидат на доводку — например, watchdog-«keepalive» или способ
> разбудить update с фонового потока. Зафиксировать при планировании M3.

## Дев-петля (как работать автономно)

Новые тулы видны server-side сразу после компиляции, но MCP-клиент — после `/mcp`-reconnect (пользователь).
Дёргай headless: `curl ... http://127.0.0.1:9730/`. Цикл: правка → headless `recompile force:true` →
Bash-поллинг `status` (isCompiling→false) → `get_logs(error)` → `run_tests`. Полный сьют через MCP
безопасен (тесты на изолированных SessionState-ключах + брекетинг). Хук brace-style требует `{}` без
однострочников и без `do...while`-форм.

## Задача: начать M3

Создать `.planning/wiki/m3-plan.md` (декомпозиция, как `m2-plan.md`), затем исполнять по волнам. Скоуп M3
(из raw F7 + долгов M2, см. `m2-plan.md` хвост):
- **F7 discoverability (полная):** `recoveryHint` во всех ответах тулов (сейчас только `status.recovery`),
  durable recovery-блок в `registry.json` (самоописываемый), опц. host-крошка в host-CLAUDE.md с согласия.
  Системы: `recovery-discoverability.md`.
- **INV-3 identity-инъекция:** `projectName` в ответы ВСЕХ тулов (сейчас только `status`) — cross-cutting
  (вероятно, обёртка в McpRouter или DispatchingToolInvoker).
- **UI-доводка дашборда** (`dashboard.md`): config UI (port range/heartbeat/footgun-тогл AllowRunCsharp),
  Reload-Domain рекомендация-кнопка (AC4.4), per-project config (ProjectSettings provider).
- **PlayMode:** `DisableDomainReload` + двухслойный бэкап `enterPlayModeOptions` (как CoplayDev) для
  PlayMode-прогонов run_tests (сейчас отложено).
- **Прогресс-стриминг** в run_tests-job (completed/total, текущий тест); v2-тулы (профайлер, packages CRUD,
  SSE-стрим прогресса).
- **Recovery-gap** (см. выше) — рассмотреть при планировании.

Порядок (предложение): сначала INV-3 (дешёвая cross-cutting обёртка, трогает все тулы) → F7 discoverability →
дашборд UI → PlayMode/прогресс. Уточнить с пользователем при создании m3-plan.

## Инварианты (не нарушать) — `raw/domain/overview.md` INV-1..5; forward-поток raw→wiki→code для изменений
намерения (F7-контент в raw уже есть — это реализация; новые намерения → raw-diff + эскалация конфликтов);
атомарность raw+wiki+code; тесты по факту реализации (TDD для сложных багфиксов/рефакторинга).
```
