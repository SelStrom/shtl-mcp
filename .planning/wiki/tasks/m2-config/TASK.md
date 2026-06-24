# TASK: T11 — config (backend)

**Status:** done (76/76 + get_config e2e). UI-доводка → M3.
**Привязка:** F2/AC2.1. Бэкенд конфига (per-machine EditorPrefs).

## Реализация

- **`ShtlMcpConfig`** расширен: `Enabled` (мастер авто-старта), `PortRangeStart`/`PortRangeCount`,
  `HeartbeatSeconds` (clamp ≥1), `AllowRunCsharp` (footgun, default false). Все — EditorPrefs.
- **`PortAllocator`** — overloads `Preferred/Allocate(path, [isFree,] rangeStart, rangeCount)` (дефолты
  9700/100 сохранены). `ResolvePort` берёт диапазон из конфига.
- **`Bootstrap.Tick`** — интервал из `HeartbeatSeconds`.
- **`get_config`** (read-only тул): снимок {enabled, portRangeStart/Count, heartbeatSeconds, allowRunCsharp,
  port, serverName}. Снимок инжектируется в тул `Func<JObject>` из Lifecycle → Tools не зависит от Lifecycle.

**Footgun-модель:** `AllowRunCsharp` меняет ТОЛЬКО человек (EditorPrefs/UI в M3), не через MCP-тул.
`get_config` — read-only.

## Верификация

- Unit (`ShtlMcpConfigTests`, 3): round-trip PortRange+AllowRunCsharp; HeartbeatSeconds clamp 0→1;
  PortAllocator custom-range в [8000,8050). `Enabled` не трогаем (иначе watchdog остановил бы live-сервер). ✅
- 76/76; e2e `get_config` → корректный снимок (port 9730, allowRunCsharp false).

## Долги
- Per-project конфиг + UI (ProjectSettings provider) → M3. INV-3 — общий долг.
