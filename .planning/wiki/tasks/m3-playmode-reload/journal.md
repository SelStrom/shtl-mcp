# Journal — m3-playmode-reload (T5, append-only)

## [2026-06-25] реализация
PlayModeOptionsGuard — структурная копия TestRunnerNoThrottle (двухслойный бэкап SessionState+диск,
Apply/Restore/RecoverOnLoad), но для `EditorSettings.enterPlayModeOptions |= DisableDomainReload`. Хуки:
RunTestsTool(mode=PlayMode)→Apply, RunFinished→Restore (идемпотентно для EditMode), EnsureStarted+Bootstrap
(disabled)→RecoverOnLoad. Unit-тесты (4) зеркалят no-throttle-тесты, снимок реальных EditorSettings в OneTime.
92/92. Реальный PlayMode-прогон e2e отложен (нет PlayMode-тестов в TestProject; это инфраструктура для
PlayMode-тестов пользователя). Реализация F4 — raw не менялся.
