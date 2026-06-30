# M4 — план вехи: закрыть последние AC спеки + опц. полировка

> Декомпозиция M4 на bite-sized таски (как `m2/m3-plan.md`). Каждый таск при исполнении получает папку
> `wiki/tasks/<slug>/` (TASK.md + journal.md). Источники: `raw/features/F2,F3,F5,F7`, `raw/domain/overview.md`
> (INV-1..5), `wiki/systems/{dashboard,recovery-discoverability,lifecycle-and-reload}.md`, долги M3
> (`m3-plan.md` хвост).

## Ключевой вывод по объёму (заземление на raw)

После M3 в **raw-спеке F1–F7 остались всего ДВА невыполненных AC**:
- **AC5.5** — дашборд: хвост последних N MCP-вызовов (время, метод, статус ✓/✗, длительность). Не реализован
  (есть только `LastRequestAgeSeconds` — одна метка; `LogBuffer` — это Unity-консоль, не история вызовов).
- **AC7.4** — opt-in host-крошка (последний кусок F7): дашборд может **предложить** добавить в host-проект
  однострочный recovery-указатель (в его `CLAUDE.md`) либо минимальный recovery-скилл. По умолчанию —
  **ничего** (INV-2), только с явного согласия.

Всё остальное из «долгов M3» — НЕ жёсткие долги:
- **per-project config** — AC2.1 явно допускает «ProjectSettings-asset **или** EditorPrefs»; текущий
  EditorPrefs уже удовлетворяет AC2.1. ProjectSettings-provider — переносимость-рефайнмент, не пробел.
- **v2-тулы** (профайлер, packages CRUD, материалы/шейдеры, reflection-API) — F3 «Out of scope (v2+,
  достижимо через escape hatches)». Покрыты `run_csharp`/`execute_menu_item`. Не пробел — добавлять только
  под конкретную повторяющуюся нужду.
- **recovery-gap** (глубокий idle-троттлинг не будится HTTP/control-flag) — **не в raw**. Изменение
  поведения → требует forward-потока (сначала AC). Срочность снижена: зависания этой сессии были модальные
  (закрыты scenePolicy/modal-free), а `ping` (M3) idle-троттлинг хотя бы **детектит**.

**Итог:** закрыть AC5.5 + AC7.4 = вся спека F1–F7 реализована (v1 feature-complete). Остальное —
дискреционная полировка.

## Объём M4

- **AC5.5 call-tail** (F5) — реальный долг.
- **AC7.4 host-крошка** (F7) — реальный долг, opt-in.
- **per-project config** (F2) — опц. рефайнмент (по желанию пользователя).
- **recovery-gap keepalive** (надёжность) — кандидат, forward-поток (сначала raw).

## Таски и зависимости

| # | Таск (slug) | Содержание | Forward-поток? | Зависит |
|---|---|---|---|---|
| **T1** | `m4-call-tail` | AC5.5: ring-buffer последних N MCP-вызовов на сервере (метод, статус ✓/✗, длительность, время) — инструментировать единую точку (`McpRouter.Handle` / `DispatchingToolInvoker`); рендер хвоста в дашборде (живой апдейт через уже существующий `Update()`-цикл). Транзиентно (in-memory), персист не нужен. | Нет (реализация F5/AC5.5, raw есть) | — |
| **T2** | `m4-host-breadcrumb` | AC7.4: дашборд-кнопка «Add recovery breadcrumb to host project» — с явным подтверждением пишет однострочный recovery-указатель в host-`CLAUDE.md` (или минимальный recovery-скилл). По умолчанию ничего (INV-2). Определить корень host-проекта; идемпотентно (не дублировать); показать что именно будет записано до согласия. | Реализация F7/AC7.4 (raw есть); UX-детали свободно | — |
| **T3** | `m4-per-project-config` (опц.) | ProjectSettings-provider, зеркалящий текущий EditorPrefs-конфиг (port range/heartbeat/auto-start/footgun); `SettingsProvider` в Project Settings; правило приоритета EditorPrefs↔ProjectSettings; миграция. Конфиг едет с проектом. | Реализация F2/AC2.1 (raw есть; EditorPrefs уже валиден) | — |
| **T4** | `m4-idle-keepalive` (кандидат) | Recovery-gap: исследование, **почему** глубокий idle не будится HTTP/control-flag (оба зависят от тикающего `update`); дизайн keepalive (напр. фоновый «толчок» update / `delayCall`-heartbeat / wake-сигнал). **Сначала raw-AC** (изменение поведения), потом wiki, потом код. | **ДА** (новый AC надёжности под F2/F4) | — |

## Порядок (волны)

1. **Закрыть спеку:** **T1** (AC5.5) → **T2** (AC7.4). Независимы; после них F1–F7 полностью реализованы.
2. **Опц. полировка:** **T3** (per-project config) — по желанию.
3. **Опц. надёжность:** **T4** (idle-keepalive) — forward-поток, research-gated, ниже приоритетом.

**Критический путь:** T1, T2 (независимы). T3/T4 — дискреционные.

## Точки контроля

- T1/T2/T3 — реализация уже зафиксированного raw (F5/F7/F2); отклонения → wiki, атомарно.
- **T4 меняет НАМЕРЕНИЕ** (новый AC надёжности) → forward-поток raw→wiki→code; интент по recovery-gap
  человеком пока НЕ задан явно — перед T4 согласовать постановку (что считаем «разбудить»).
- **AC7.4 (T2) пишет в host-проект** — это outward-facing действие: только по явному согласию в моменте
  (не «durable»-разрешение), показать точный текст записи до применения.
- Не нарушать INV-1..5 (особенно INV-2 self-contained: host-крошка — opt-in, по умолчанию ничего;
  INV-4 единственное окно дашборда).
- Тесты по факту реализации (TDD для сложных багфиксов/рефакторинга).

## Прогресс (2026-07-01)

- ✅ **T1 `m4-call-tail`** (AC5.5) — `CallTail` ring-buffer (Lifecycle, делегат в роутер → DAG цел) +
  инструментирование `tools/call` (метод/ok/мс) + дашборд-foldout «Recent calls». 121/121
  (+5 CallTail, +4 router-recording).
- ✅ **T2 `m4-host-breadcrumb`** (AC7.4) — `HostBreadcrumb` (Text/IsPresent/TargetPath/AddTo, идемпотентно) +
  opt-in дашборд-foldout (превью + confirm-диалог, запись за человеком). 126/126 (+5).

- ✅ **T3 `m4-per-project-config`** — **closed by analysis (не строим):** committed per-project config —
  антипаттерн для этих настроек (footgun не должен «ездить» через VC = security; Enabled/port/heartbeat —
  machine-семантичны). EditorPrefs (machine-local) правилен, AC2.1 удовлетворён. Закреплён security-инвариант
  footgun-локальности в `ShtlMcpConfig`.
- ✅ **T4 `m4-idle-keepalive`** (AC4.10, forward-поток) — research-workflow → дизайн Option B: opt-in тогл
  (default OFF) держит No-Throttling пока сервер включён (фон не заклинивает main-thread tools + control-flag).
  `IdleKeepAlive.Reconcile` (Tools, config-агностичен) + проводка watchdog/EnsureStarted/дашборд. Best-effort
  (фоновый троттл версионно-зависим; `ping` — источник истины). 131/131 (+5, RED-gate на per-run-restore).

**🎯 M4 ЗАВЕРШЁН.** T1 (call-tail) + T2 (host-крошка) закрыли последние AC спеки → **F1–F7 feature-complete**;
T3 закрыт анализом (committed config — антипаттерн); T4 (idle-keepalive) — forward-поток, opt-in best-effort.
35 тулов (+ping), 131 EditMode-тест. Долги: bg-thread-«будилка» (эскалация AC4.10, если тогл недостаточен на
целевом LTS); v2-тулы (escape-hatch-covered).

## Не в M4 (явно отложено)

- v2-тулы (профайлер, packages CRUD, материалы/шейдеры, reflection) — F3 out-of-scope, escape-hatch-covered.
  Добавлять только под конкретную нужду, не спекулятивно.
- SSE-стрим прогресса (server-initiated) — текущий polling `get_job` достаточен; SSE добавит сложности
  транспорта без явной нужды.
