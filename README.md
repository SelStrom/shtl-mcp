# Shtl MCP

Self-contained **MCP (Model Context Protocol) server embedded in the Unity Editor** —
no external bridge process, no Node/Python. Gives LLM agents (Claude Code and other
MCP-over-HTTP clients) JSON-RPC control over the running Unity instance.

> Status: **M5 — command-set v2** (`0.5.1`). Full F1–F7 spec implemented (feature-complete):
> 44 built-in tools + custom-tool extension point. M5 closes the practical gaps found while
> dogfooding on a production project (write assets/code, component lifecycle, bulk/asset
> inspector edits, reflection calls, multi-scene, per-camera capture).
> See [CHANGELOG](CHANGELOG.md) for the history.

## Requirements
- Unity **2022.3 LTS** or newer.
- `com.unity.nuget.newtonsoft-json` (pulled in automatically as a dependency).
- Editor-only (the server runs in the Editor; nothing ships in player builds).

## Install (UPM)

Package Manager → **Add package from git URL…**:

```
https://github.com/SelStrom/shtl-mcp.git
```

or add to `Packages/manifest.json`:

```json
"com.shtl.mcp": "https://github.com/SelStrom/shtl-mcp.git"
```

The package lives at the repository root (canonical UPM layout), so only the package is
imported — the dev/test project (`TestProject~/`) and planning docs (`.planning/`) are
ignored by the Package Manager (folders suffixed `~` / prefixed `.`).

## Quick start
1. The server **auto-starts** when the Editor loads. Open **Window → Shtl MCP** for
   the dashboard (status, port, and the exact connect command).
2. Register it with your client. With Claude Code, copy the dashboard's command:
   ```
   claude mcp add --transport http unity-<project> http://127.0.0.1:<port>/mcp
   ```
   (The port is path-deterministic; the dashboard and `~/.unity-mcp/registry.json`
   always show the live value.)
3. Tools appear prefixed by the instance name, e.g. `mcp__unity-<project>__status`.

## Multi-instance
Each running Unity instance hosts its own server on its own port and registers in
`~/.unity-mcp/registry.json`. The tool prefix (`unity-<project>`, de-duplicated by
path hash for clones/worktrees) tells the model which instance a call targets.
`cat ~/.unity-mcp/registry.json` lists all live instances and ports.

## Custom tools (extend without forking)
Add your own project-specific MCP tool by dropping a class that implements `ITool` and is
marked `[McpTool]` into **your project's own Editor assembly** (its asmdef must reference
`Shtl.Mcp.Tools` and `Newtonsoft.Json`). No edit to shtl-mcp, no registration call — the
server discovers it via reflection at startup and after every domain reload.

```csharp
using Newtonsoft.Json.Linq;
using Shtl.Mcp.Tools;

[McpTool]
public sealed class GreetTool : ITool
{
    public string Name => "greet";
    public string Description => "Return a greeting for the given name.";
    public bool NeedsMainThread => false; // true if you touch UnityEditor/UnityEngine APIs

    public JObject InputSchema => new JObject {
        ["type"] = "object",
        ["properties"] = new JObject { ["name"] = new JObject { ["type"] = "string" } },
        ["required"] = new JArray { "name" }
    };

    public JObject Invoke(JObject args)
    {
        var name = (string)args["name"];
        if (string.IsNullOrEmpty(name)) return new JObject { ["error"] = "name is required" };
        return new JObject { ["greeting"] = "Hello, " + name + "!" };
    }
}
```

Rules: public parameterless constructor; return a structured `{ "error": ... }` instead of
throwing; a custom tool whose `Name` collides with a built-in is rejected (built-ins win); a
broken tool is skipped with a console warning without stopping the server. `projectName` and
`recoveryHint` are added to the response automatically. After adding a tool, reconnect the MCP
client to refresh its tool list. Working example: `TestProject~/Assets/Editor/HostMcpTools/`.

## Tools
44 built-in tools; the live list (with schemas) comes from `tools/list` after connecting.

| Category | Tools |
|----------|-------|
| Diagnostics | `status`, `ping` (answers even when the main thread is blocked), `get_logs`, `clear_logs`, `get_config`, `get_job` |
| Lifecycle (reload-spanning jobs) | `recompile`, `set_play_mode`, `run_tests` (EditMode/PlayMode) |
| Assets | `refresh_assets`, `find_assets`, `read_asset`, `write_asset` (text assets & scripts; compiled extensions go through the reload job, compile errors via `get_job`), `move_asset`, `delete_asset`, `create_folder` |
| Prefabs | `create_prefab`, `open_prefab`, `save_prefab`, `close_prefab`, `instantiate_prefab` |
| Scenes (multi-scene) | `open_scene`, `save_scene`, `list_scenes`, `create_scene`, `unload_scene`, `set_active_scene` |
| GameObjects & components | `get_hierarchy`, `find_gameobject`, `gameobject_create`, `gameobject_destroy`, `gameobject_modify`, `add_component`, `remove_component`, `get_object`, `modify_object` (bulk + nested property paths, targets scene objects / assets / instanceIds), `set_parent`, `get_selection`, `set_selection` (scene objects or asset paths) |
| Reflection | `call_method` (existing C# methods, static/instance incl. private), `find_method` (overload discovery) |
| Capture | `screenshot` (Game/Scene view, a named camera, or `overlay:true` for the composited Screen-Space UI in Play mode — as MCP image content), `screenshot_uxml` (render a UXML asset to a PNG offscreen in Edit mode, no Play mode) |
| Escape hatches | `execute_menu_item`, `run_csharp` (footgun-gated, human-only) |

## Reliability
The server survives script recompilation and play/edit transitions: the listener is
cleanly stopped before a domain reload and re-spawned after (`[InitializeOnLoad]` +
`AssemblyReloadEvents`), with the port persisted in `SessionState` and a per-second
watchdog that re-binds if needed.

## Development
This repository **is** the package (canonical UPM layout: `package.json`, `Editor/`,
`Tests/` at the root). The dev/test Unity project lives in `TestProject~/` and
references the package via `file:../../` + `testables`. The test assembly is
additionally gated by the `SHTL_MCP_DEV` scripting define (set in TestProject~'s
Player settings) so package tests never compile in consumer projects — even when
the package is embedded or referenced by a local `file:` path. Run EditMode tests:

```bash
UNITY="/Applications/Unity/Hub/Editor/2022.3.62f3/Unity.app/Contents/MacOS/Unity"
"$UNITY" -batchmode -nographics -runTests -projectPath "TestProject~" \
  -testPlatform EditMode -testResults /tmp/results.xml -logFile -
```

Intent-driven planning docs (`raw`/`wiki`, see [`CLAUDE.md`](CLAUDE.md)) live in
`.planning/` — ignored by UPM, not shipped to consumers.

## License
[MIT](LICENSE.md). Project home & docs: https://github.com/SelStrom/shtl-mcp
