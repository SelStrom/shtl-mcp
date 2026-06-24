# Journal — m2-config (T11, append-only)

## [2026-06-24] backend
ShtlMcpConfig расширен (PortRange/Heartbeat/AllowRunCsharp, EditorPrefs); PortAllocator получил
range-overloads (дефолты сохранены), ResolvePort и Bootstrap.Tick читают конфиг. get_config (read-only)
со снимком, инжектируемым Func'ом из Lifecycle (Tools не зависит от Lifecycle — DAG цел). Footgun
AllowRunCsharp меняет только человек. Верификация: 76/76 (+3: round-trip, clamp, port-range), e2e
get_config. UI → M3. raw не менялся (F2/AC2.1 — реализация).
