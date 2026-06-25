# TASK: T5 — PlayMode DisableDomainReload

**Status:** done (guard + хук + unit-тесты; PlayMode-прогон e2e отложен — нет PlayMode-тестов)
**Привязка:** F4 (стабильность PlayMode-прогона). Реализация — raw не менялся.

## Реализация

- **`PlayModeOptionsGuard`** (зеркалит проверенный TestRunnerNoThrottle): форсит `EditorSettings.
  enterPlayModeOptions |= DisableDomainReload` на время PlayMode-прогона (вход в Play НЕ выгружает домен →
  listener/прогон не гибнут). **Двухслойный бэкап** исходного enterPlayModeOptions: SessionState
  (переживает reload) + marker-файл `Library/` (переживает краш — иначе редактор останется с форсированным
  DisableDomainReload). Apply/Restore/RecoverOnLoad.
- **Хуки:** `RunTestsTool` при `mode=PlayMode` → `Apply`; `TestRunCallbacks.RunFinished` → `Restore`
  (идемпотентно: EditMode-прогон не трогал); `ShtlMcpServer.EnsureStarted` + `Bootstrap.Init` (disabled) →
  `RecoverOnLoad`.

## Верификация

- Unit (`PlayModeOptionsGuardTests`, 4): Apply ставит DisableDomainReload; Restore возвращает baseline;
  crash-recovery с диска после потери SessionState; recover-on-pending держит DisableDomainReload. Реальные
  EditorSettings снимаются/возвращаются в OneTime. **92/92** (88 + 4).
- **Отложено:** e2e реального PlayMode-прогона — в TestProject нет PlayMode-тестов (guard — инфраструктура
  для PlayMode-тестов host-проекта; добавление PlayMode-тест-сборки → M4).
