---
entity: dashboard
content_class: intent-derived
source_refs:
  - raw/features/F5-dashboard.md
compiled_at_commit: pending
epoch: 001
status: active
needs_review: false
---

# UI-дашборд

Единственное окно (`EditorWindow` на UI Toolkit). Информационное, минимальное.
Не открывается принудительно — только из пункта меню (напр. `Window/Shtl MCP`).

## Макет

```
┌─ Unity MCP ───────────────────────────────┐
│  ● running   unity-PerfectWar   :9712      │
│  mode: EDIT      clients: 1   uptime 12m   │
│                                            │
│  claude mcp add command:        [copy]     │
│  claude mcp add --transport http \         │
│    unity-PerfectWar http://127.0.0.1:9712… │
│                                            │
│  ⚠ Reload Domain on Play: ON  [fix]        │
│                                            │
│  recent:                                   │
│   12:00:03  tools/call recompile  ✓ 1.2s   │
│   12:00:01  get_logs              ✓        │
│                                  [Restart] │
└────────────────────────────────────────────┘
```

## Элементы (и связь с acceptance)
- **Светофор статуса** ● running / ○ stopped — AC5.2.
- **Идентичность:** `serverName`, `port`, `mode` (edit/play), `clients`, `uptime`
  — AC5.2.
- **Строка подключения** `claude mcp add …` + кнопка **copy** — AC5.3.
- **Предупреждение Reload Domain on Play** + кнопка **fix** (применить OFF с
  согласия) — AC5.4 / AC4.4.
- **Хвост последних N вызовов** (время, метод, ✓/✗, длительность) — AC5.5.
- **Restart server** (force respawn) — AC5.6 / AC2.4.
- Больше ничего; никаких иных окон/попапов — AC5.7 / INV-4.

## Поведение
- Обновляется по тику (тот же `EditorApplication.update`, что heartbeat).
- Read-only по сути, кроме трёх действий: copy, fix, restart.
- Конфиг-поля (диапазон портов, авто-старт, включение `run_csharp`) — компактная
  сворачиваемая секция «Settings» (F2/AC2.1), скрытая по умолчанию, чтобы дашборд
  оставался минимальным.
