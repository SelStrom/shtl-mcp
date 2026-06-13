# Changelog

All notable changes to this package are documented here. Format follows
[Keep a Changelog](https://keepachangelog.com/en/1.1.0/); versioning is
[SemVer](https://semver.org/).

## [0.1.0] — 2026-06-13

Milestone **M1 — Walking Skeleton**. First working vertical slice.

### Added
- In-Unity MCP server over Streamable HTTP (`HttpListener` + JSON-RPC 2.0):
  `initialize`, `tools/list`, `tools/call`. No external bridge process.
- Background HTTP thread with main-thread marshaling (`MainThreadDispatcher`
  drained on `EditorApplication.update`) — Unity API touched only on the main thread.
- Domain-reload survival: `[InitializeOnLoad]` + `AssemblyReloadEvents`
  (before/after) + `SessionState` port persistence + per-second watchdog;
  resilient listener rebind on busy port.
- Multi-instance routing: path-deterministic port, server-name dedup by path hash,
  registry at `~/.unity-mcp/registry.json` (atomic write, heartbeat, prune).
- Tools: `status` (instance identity/mode/health), `get_logs` (recent console logs).
- Minimal UI Toolkit dashboard (`Window/Shtl MCP`): status, ready-to-paste
  `claude mcp add` command, Restart.
- 34 EditMode tests covering the pure-logic core.

### Not yet (roadmap)
- M2: full tool set (assets/prefabs/scene/hierarchy), async-job for
  `recompile`/`set_play_mode`/`run_tests`, `run_csharp`/`execute_menu_item`,
  `screenshot`, control-flag restart.
- M3: recovery discoverability (self-documenting registry, pre-briefing,
  opt-in host breadcrumb), dashboard polish.
