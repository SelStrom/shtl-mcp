# TASK: Наблюдаемость re-spawn в status (reloadCount / listenerUptimeSeconds)

**Status:** done (все AC ✓: юнит+integration зелёные, live `reloadCount:1`/`listenerUptimeSeconds` сброс)
**Привязка:** raw **F4/AC4.7** (новый — наблюдаемость reload-survival), F3 (контракт `status`),
поддерживает INV-5 (самовосстановление — делает его наблюдаемым). Системы:
`wiki/systems/{lifecycle-and-reload, command-set}.md`.

## Контекст (зачем)

Сегодняшняя ручная reload-приёмка показала: `status` структурно не отличает re-spawn — `uptimeSeconds`
durable (от `StartedTicks`, ставится раз за сессию; переживает reload и RestartNow), pid/port не
меняются. Headline-риск M1 был неверифицируем по live-каналу. Эта правка делает re-spawn наблюдаемым
в проде (полезно для F7-recovery и для приёмки всего M2).

## Изменение поведения (контракт status)

Два новых поля в ответе `status`:
- **`reloadCount`** (int) — число пережитых domain reload за сессию Unity. Инкремент в
  `beforeAssemblyReload` (`ShtlMcpServer.NoteReloadStarting`), durable в `SessionState`
  (`Shtl.Mcp.ReloadCount`). Переживает reload.
- **`listenerUptimeSeconds`** (int) — время жизни текущего listener-инстанса. Ставится в каждом
  успешном `EnsureStarted` (`_listenerStartedUtc`); in-domain, не persisted → естественно
  сбрасывается при каждом re-spawn (reload / watchdog-rebind / RestartNow).

Различение сигналов: reload → `reloadCount`↑ + `listenerUptimeSeconds` сброс; watchdog-rebind →
только `listenerUptimeSeconds` сброс. Дополняют durable `uptimeSeconds` (логическое время инстанса).

## Forward-поток (атомарно)

- **raw:** F4/AC4.7 (новый AC).
- **wiki:** `command-set.md` (status-строка), `lifecycle-and-reload.md` (§1 счётчики + точки
  верификации обновлены под реализованный тест).
- **code:** `IEditorContext` (+2 члена), `EditorContext` (+2 Func/props), `ShtlMcpServer`
  (`ReloadCountKey`, `_listenerStartedUtc`, `ListenerUptime`/`ReloadCount`, `NoteReloadStarting`,
  отметка в `EnsureStarted`, проводка в ctx), `ShtlMcpBootstrap.OnBeforeReload` (инкремент),
  `StatusTool` (+2 поля).
- **Проверка консистентности:** конфликта с INV-1..5 нет; поддерживает INV-5. Раздвоения интента нет.

## Acceptance

- AC-1: `status` отдаёт `reloadCount` и `listenerUptimeSeconds`.
- AC-2: `StatusToolTests` зелёный (юнит: новые поля в ответе через `FakeContext`).
- AC-3: `Listener_Respawns_AfterDomainReload` зелёный и **дополнительно** проверяет, что `reloadCount`
  вырос после reload (RED-gate для самой фичи: без `NoteReloadStarting` счётчик не растёт → красное).
- AC-4 (live): после ≥2 reload в живом редакторе `status.reloadCount ≥ 1`, `listenerUptimeSeconds`
  заметно меньше `uptimeSeconds`.

## Шаги

1. raw+wiki+code+тесты (сделано).
2. Компиляция: `get_logs(error)` чисто после рекомпиляции.
3. Прогон Test Runner (EditMode): `StatusToolTests` + `ReloadSurvivalTests` зелёные.
4. Live-проверка: `status` показывает новые поля; после второго reload `reloadCount` растёт.
5. Финал: index/log, статус → done.

## Заметка

Первая рекомпиляция (внедрение этой правки) reloadCount НЕ инкрементит — `beforeAssemblyReload`
на ней исполняет ещё старый код (без `NoteReloadStarting`). Счётчик начинает расти со следующего
reload. Для live-AC-4 нужен второй reload.
