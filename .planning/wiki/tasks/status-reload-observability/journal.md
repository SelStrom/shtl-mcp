# Journal — status-reload-observability (append-only)

## [2026-06-14] forward | raw+wiki+code

Мотивация: сегодняшняя ручная reload-приёмка уперлась в durable `uptimeSeconds` — `status` не
отличает re-spawn. Пользователь выбрал направление «(2) наблюдаемость status, потом (1) M2;
дальше самостоятельно, пробелы — в доработках».

Проверка консистентности (INV-1..5): новые поля поддерживают INV-5 (самовосстановление наблюдаемо),
не конфликтуют с INV-1/2/3/4, интент не раздваивается. Дом интента — F4 (наблюдаемость reload-survival),
т.к. сегодня именно AC4.1 оказался неверифицируем вручную. Конфликта нет → forward без эскалации.

raw: F4/AC4.7. wiki: command-set.md (status-строка), lifecycle-and-reload.md (§1 счётчики + точки
верификации обновлены под реализованный m1-reload-survival-test). code: IEditorContext (+ListenerUptimeSeconds,
+ReloadCount), EditorContext (+2 Func/props), ShtlMcpServer (ReloadCountKey, _listenerStartedUtc,
ListenerUptime/ReloadCount, NoteReloadStarting, отметка в EnsureStarted, проводка в ctx),
ShtlMcpBootstrap.OnBeforeReload (NoteReloadStarting перед StopListenerForReload), StatusTool (+2 поля).
Тесты: StatusToolTests (FakeContext +2 члена, +2 ассерта), McpProbe (TryGetStatus + рефактор
TryGetProjectName), ReloadSurvivalTests (захват reloadCount до reload, ассерт роста после).

Сверка реализаторов IEditorContext (оба обновлены) и call-site new EditorContext (один, обновлён) —
ок. Дальше: компиляция (get_logs error) + прогон Test Runner + live-проверка status.

## [2026-06-14] верификация | done

Компиляция чистая (get_logs error пуст). Live после первой рекомпиляции: `listenerUptimeSeconds: 37`,
`reloadCount: 0` (ожидаемо — beforeAssemblyReload отработал старым кодом). Прогон Test Runner (EditMode,
пользователь): `StatusToolTests` + `ReloadSurvivalTests` — все зелёные (включая ассерт роста reloadCount
после reload). Live после прогона: `reloadCount: 1`, `listenerUptimeSeconds: 28` vs `uptimeSeconds: 54755`
(durable, ~15ч — редактор открыт со вчера). AC-1..4 выполнены. Таск done.
