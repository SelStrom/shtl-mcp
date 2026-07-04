# Changelog

All notable changes to this package are documented here. Format follows
[Keep a Changelog](https://keepachangelog.com/en/1.1.0/); versioning is
[SemVer](https://semver.org/).

## [0.5.0] — 2026-07-04

Milestone **M5 — command-set v2**. Closes the practical gaps found while dogfooding on a production
project as a replacement for `com.ivanmurzak.unity.mcp` (F3/AC3.9–3.14): 44 built-in tools.

### Added
- **`write_asset` (AC3.9):** create/overwrite text assets under `Assets/` (`.cs`, `.uxml`, `.uss`,
  `.json`, `.asmdef`, ...) — the pair of `read_asset`. Compiled extensions (`.cs`/`.asmdef`/`.asmref`)
  go through the recompile reload-job: returns a `jobId`, compile errors are delivered via `get_job`.
  A regular tool, not footgun-gated (writing code is not ad-hoc execution; decided by the human).
- **`add_component` / `remove_component` (AC3.10):** component lifecycle on scene GameObjects. Type
  resolution by full or unique short name with suggestions; clear errors for abstract types,
  `DisallowMultipleComponent` duplicates and `RequireComponent` dependents; `index` disambiguates
  same-type components; `Transform` is protected.
- **`call_method` + `find_method` (AC3.12, reflection):** call an existing C# method (static/instance,
  including private) by type and signature; `find_method` lists signatures for overload picking.
  `UnityEngine.Object` parameters accept scene paths / asset paths / instanceIds. Not footgun-gated.
- **Multi-scene (AC3.13):** `list_scenes` (path/isLoaded/isDirty/isActive), `create_scene` (additive,
  optional save-as; the new scene becomes active), `unload_scene`, `set_active_scene` (idempotent).
- **Custom tools without forking (AC3.6–3.8, previously unreleased):** host projects add `[McpTool]`
  `ITool` classes in their own Editor assembly; discovered via `TypeCache` after built-ins, broken
  tools are skipped with a warning, built-ins win name collisions.

### Changed
- **`get_object` / `modify_object` (AC3.11):** targets are now scene GameObjects (path/name), asset
  paths (`Assets/...`) or instanceIds — ScriptableObjects, materials and other assets are inspectable
  and editable (persisted to disk). `modify_object` accepts a bulk `changes` array with nested
  property paths (`m_Size.x`) applied as one all-or-nothing transaction. `get_object` expands nested
  structs/arrays up to `maxDepth` within a response budget.
- **`screenshot` (AC3.14):** new `camera` parameter captures a specific camera by GameObject
  path/name (takes priority over `view`).

### Notes
- 184 EditMode tests (46 new), including a reload-spanning `write_asset` e2e over real HTTP.
- Long-tail items stay on escape hatches by design (F3): materials/shaders specifics, packages CRUD,
  `type-get-json-schema`, built-in resources, profiler.

## [0.4.0] — 2026-07-01

Milestone **M4 — spec completion + reliability**. The full F1–F7 spec is now implemented (feature-complete).

### Added
- **Call-tail (AC5.5):** dashboard "Recent calls" — last N MCP calls with method, ✓/✗ status, duration, age.
- **Opt-in host recovery breadcrumb (AC7.4):** dashboard offers to append a one-line recovery pointer to the
  host project's `CLAUDE.md` — only after explicit confirmation, with a preview. Default: nothing (INV-2).
- **idle-keepalive (AC4.10):** opt-in toggle keeps the editor in No-Throttling while the server is enabled, so
  a backgrounded/unfocused editor keeps ticking (MCP tools + control-flag stay reachable). Best-effort /
  version-dependent; `ping` (AC4.8) stays the source of truth. Default OFF.

### Changed
- `get_config` now exposes `idleKeepAlive`.
- Config stays machine-local (`EditorPrefs`) by design — the `run_csharp` footgun must not travel via VCS.

### Notes
- 131 EditMode tests. Final adversarial review: 0 blocker/major.

## [0.3.0] — 2026-06-26

Milestone **M3 — discoverability + reliability**.

### Added
- **bg-liveness `ping` (AC4.8):** answers on the background thread even when the main thread is blocked
  (modal / compile) — distinguishes a wedged main thread from a dead server.
- **Modal-free PlayMode runs (AC4.9):** `run_tests mode=PlayMode` resolves a dirty scene by policy
  (`scenePolicy`: discard/save/abort) instead of Unity's blocking Save-scene dialog.
- **INV-3 identity:** `projectName` injected into every tool response.
- **F7 discoverability:** durable per-instance `recovery` block in `registry.json`, strengthened
  `initialize.instructions`, `recoveryHint` in every response.
- **Dashboard polish:** one-button "Add to Claude Code", live LLM-client status, Reload-Domain toggle,
  config foldout.
- PlayMode `DisableDomainReload` guard; `run_tests` progress streaming via `get_job`.

### Fixed
- Code-review remediation: `JobStore.Get` returns a consistent snapshot; Host/Origin filter hardened
  (fail-closed) with tests; PlayMode run verified end-to-end.

## [0.2.0] — 2026-06-25

Milestone **M2 — full Core MCP toolset**.

### Added
- **Async-job model:** `JobStore` (in `SessionState`, survives domain reload) + `get_job`.
- **Reload-spanning commands:** `recompile`, `set_play_mode`; `run_tests` (EditMode/PlayMode) with
  focus no-throttle + orphan timeout.
- **Assets:** `clear_logs`, `refresh_assets`, `find_assets`, `read_asset`, `move_asset`, `delete_asset`,
  `create_folder`.
- **Prefabs:** create / open / save / close / instantiate.
- **Scene & GameObjects:** hierarchy CRUD, `set_parent`, `find_gameobject`, `get/modify_object`,
  `open/save_scene`, selection.
- **`screenshot`** (game/scene) as MCP image content.
- **Escape hatches:** `execute_menu_item`, `run_csharp` (footgun-gated, human-only).
- Config backend (`get_config`) + control-flag restart.
- 6-asmdef split (Transport / Dispatcher / Registry / Tools / Lifecycle / UI).

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
