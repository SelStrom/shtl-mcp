# Journal — m3-dashboard-ui (T4, append-only)

## [2026-06-25] реализация
DashboardWindow расширен: Reload-Domain статус+кнопка (AC4.4, EditorSettings.enterPlayModeOptions |=
DisableDomainReload) + config-секция (Enabled/AllowRunCsharp-footgun/PortRange/Heartbeat, биндинг на
ShtlMcpConfig через RegisterValueChangedCallback). Footgun-тогл в UI = тот самый human-only гейт run_csharp.
Хелперы BindToggle/BindInt/Header. UI→Lifecycle (ShtlMcpConfig/ShtlMcpServer) — DAG сборок цел.
Компиляция чистая (UI.dll), 88/88. UI — glue, тестов нет; визуальный осмотр за человеком (Window/Shtl MCP) —
screenshot снимает камеры, не editor-окна. Per-project ProjectSettings provider + host-крошка (AC7.4) —
отложены. Реализация F2/AC2.1, F4/AC4.4 — raw не менялся.

## [2026-06-26] доводка по фидбэку (компактность + UX)

Дашборд стал некомпактным. Правки:
1. **MCP — одной кнопкой** «Add to Claude Code» (`claude mcp add --transport http --scope user <name> <url>`,
   через login-shell асинхронно — не блокирует main/dispatcher). Если `claude mcp list` содержит наш
   serverName → прячем кнопку, показываем «✓ added to Claude Code». Готовая команда — в свёрнутом
   «Manual add command» (fallback). Детект асинхронный (фоновый поток → delayCall), таймаут 8с (list делает
   health-check). claude найден через login-shell (PATH GUI-приложения минимален).
2. **Reload Domain** — убрана навязчивая «recommended»; тогл «Disable domain reload on Enter Play» со
   статусом, вся аргументация (зачем: непрерывная доступность MCP без re-spawn-паузы; компромисс: статики
   не сбрасываются → [InitializeOnEnterPlayMode]) — в **tooltip**.
3. **Config — в `Foldout`** (свёрнут по умолчанию): Enabled/AllowRunCsharp(+tooltip)/port range/heartbeat +
   Restart. Дашборд снова компактный.

Компиляция чистая; логика детекта проверена (claude mcp list показывает unity-shtl-mcp → будет «✓»).
Визуальный осмотр — за человеком (Window/Shtl MCP, переоткрыть окно для нового layout).

## [2026-06-26] доводка 2 (по визуальному фидбэку + баг-фиксы)

- **claude не находился** (детект показывал кнопку Add при подключённом MCP): Unity (GUI) запускает процесс
  с минимальным PATH, а login-shell его не чинит (`~/.local/bin` не в PATH → claude NOTFOUND, exit 127).
  Диагностировано через `env -i`-мимикрию. Фикс: резолвлю бинарь `claude` НАПРЯМУЮ по типичным путям
  (`ClaudeBin`: ~/.local/bin, homebrew, …), без shell; детект по наличию имени в выводе, не строго exit==0.
- **Баг threading (ошибка в консоли):** `EditorApplication.delayCall += …` дёргался из фонового потока
  (Task.Run) — Unity API вне главного потока. Фикс: результат async-claude → потокобезопасная
  `ConcurrentQueue`, дренаж в `Update()` (главный поток).
- **Manual add command** скрывается вместе с кнопкой Add (когда настроено — оба не нужны).
- **Отступ Settings/Manual** уменьшен (убран верхний margin, content-indent foldout ~15px → 2px, хелпер Indent).
- **Строка `LLM client`** — живой коннект (по времени последнего HTTP-запроса, `ShtlMcpServer.LastRequestAgeSeconds`):
  active(<30с)/idle/none. Семантика request-based (streamable-HTTP без keepalive — «active» = недавний запрос).
  Три уровня раздельно: server running / LLM client active-idle / ✓ added (конфиг).

Верифицировано: компиляция чистая, 94/94 не тронуты; e2e — прямой бинарь в Unity-подобном env выдаёт
unity-shtl-mcp; ping через MCP-клиент → projectName/recoveryHint в ответе. Визуал — за человеком (подтверждён).
