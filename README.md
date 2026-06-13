# Shtl MCP

Self-contained **MCP (Model Context Protocol) server embedded in the Unity Editor** —
no external bridge process, no Node/Python. Gives LLM agents (Claude Code and other
MCP-over-HTTP clients) JSON-RPC control over the running Unity instance.

> Status: **M1 — Walking Skeleton** (`0.1.0`). Core transport + lifecycle + 2 tools.
> See [CHANGELOG](CHANGELOG.md) for the roadmap.

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

## Tools (M1)
| Tool | Description |
|------|-------------|
| `status` | Instance identity (project, path, version, port, pid), mode (edit/play), `isCompiling`, health. |
| `get_logs` | Recent Unity console logs; filter by `minLevel` (info/warning/error) and `count`. |

## Reliability
The server survives script recompilation and play/edit transitions: the listener is
cleanly stopped before a domain reload and re-spawned after (`[InitializeOnLoad]` +
`AssemblyReloadEvents`), with the port persisted in `SessionState` and a per-second
watchdog that re-binds if needed.

## Development
This repository **is** the package (canonical UPM layout: `package.json`, `Editor/`,
`Tests/` at the root). The dev/test Unity project lives in `TestProject~/` and
references the package via `file:../../` + `testables`. Run EditMode tests:

```bash
UNITY="/Applications/Unity/Hub/Editor/2022.3.62f3/Unity.app/Contents/MacOS/Unity"
"$UNITY" -batchmode -nographics -runTests -projectPath "TestProject~" \
  -testPlatform EditMode -testResults /tmp/results.xml -logFile -
```

Intent-driven planning docs (`raw`/`wiki`, see [`CLAUDE.md`](CLAUDE.md)) live in
`.planning/` — ignored by UPM, not shipped to consumers.

## License
[MIT](LICENSE.md). Project home & docs: https://github.com/SelStrom/shtl-mcp
