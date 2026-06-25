# Journal — m3-dashboard-ui (T4, append-only)

## [2026-06-25] реализация
DashboardWindow расширен: Reload-Domain статус+кнопка (AC4.4, EditorSettings.enterPlayModeOptions |=
DisableDomainReload) + config-секция (Enabled/AllowRunCsharp-footgun/PortRange/Heartbeat, биндинг на
ShtlMcpConfig через RegisterValueChangedCallback). Footgun-тогл в UI = тот самый human-only гейт run_csharp.
Хелперы BindToggle/BindInt/Header. UI→Lifecycle (ShtlMcpConfig/ShtlMcpServer) — DAG сборок цел.
Компиляция чистая (UI.dll), 88/88. UI — glue, тестов нет; визуальный осмотр за человеком (Window/Shtl MCP) —
screenshot снимает камеры, не editor-окна. Per-project ProjectSettings provider + host-крошка (AC7.4) —
отложены. Реализация F2/AC2.1, F4/AC4.4 — raw не менялся.
