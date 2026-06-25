# TASK: T4 — dashboard UI

**Status:** done (логика + компиляция; визуальный осмотр — за человеком: Window/Shtl MCP)
**Привязка:** F2/AC2.1 (config UI), F4/AC4.4 (Reload-Domain кнопка). Реализация — raw не менялся.

## Реализация (`DashboardWindow`, UI Toolkit)

К существующему (status/identity/mode + claude mcp add + Copy/Restart) добавлено:
- **Reload-Domain (AC4.4):** статус «Reload Domain: ON/OFF» (`EditorSettings.enterPlayModeOptions &
  DisableDomainReload`) + кнопка «Apply recommended (Reload Domain OFF)» (ставит
  `enterPlayModeOptionsEnabled=true` + `|= DisableDomainReload`).
- **Config UI** (per-machine EditorPrefs через ShtlMcpConfig, биндинг на изменение):
  Server enabled (auto-start), **Allow run_csharp ⚠ FOOTGUN** (тогл здесь = human-only гейт), Port range
  start/count, Heartbeat seconds.

## Верификация

- Компиляция чистая (`Shtl.Mcp.UI.dll`), 88/88 (UI — glue, без юнит-тестов по принципу).
- **Визуальный осмотр — за человеком** (`Window/Shtl MCP`): мой `screenshot` снимает game/scene-камеры,
  не editor-окна.

## Долги
- **Per-project config (ProjectSettings provider)** — отложено: смена модели хранения (EditorPrefs
  per-machine → ScriptableObject per-project). Опц. M3-добивка / M4.
- AC7.4 host-крошка (из T3) — кнопка-предложение в дашборде с явным согласием — отложена вместе с per-project.
