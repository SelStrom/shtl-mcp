# Autonomous MCP Recovery & Multi-Instance-Safe Lifecycle — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Make the server re-establish its HTTP listener and honor restart requests with zero human action (no window focus, no modal dismissal, no manual recompile), while keeping multi-instance identity (serverName/port/registry) and `--scope user` global registration correct.

**Architecture:** A dedicated background watchdog thread owns listener liveness (re-bind on the instance's own port), the `.cmd` control channel, and heartbeat — none of which then depend on `EditorApplication.update`, which Unity throttles when the window is unfocused and freezes entirely on a modal dialog. The Editor update loop keeps only main-thread-bound work: server setup/registration and registry-aware port/name (re)allocation. Registry writes become cross-process-safe via a file lock so concurrent background heartbeats from multiple instances don't lose entries. A port change re-runs `claude mcp add --scope user` so the global registration never points at a stale port.

**Tech Stack:** Unity 6.x Editor, C#, `System.Net.HttpListener`, `System.Threading`, EditorPrefs/SessionState, NUnit.

## Global Constraints

- Tests live in the `TestProject~/` NUnit assembly gated by `SHTL_MCP_DEV`; package consumers never compile them. Unit tests here are pure C# (no Unity API) so they run in EditMode Test Runner.
- No new external dependencies — stdlib only.
- `serverName` and `port` are the multi-instance identity AND the `--scope user` registration key — they MUST stay stable across domain reload and MUST never change silently.
- Server config stays machine-local (`EditorPrefs`), never a committed asset.
- The background thread MUST NOT touch `EditorApplication.*` / `UnityEngine.*` main-thread API. It may only do pure C#, file IO, and `HttpServer` bind/stop. Anything main-thread is marshalled via `MainThreadDispatcher`.
- Thread-shared fields read by the watchdog (`_http`, `_serverName`, `Port`) must be `volatile` or accessed through `Interlocked`/locks.

---

## File Structure

- `Editor/Transport/HttpServer.cs` — add `Abort()` for immediate port release; make the listener object survive a failed bind so it can be retried.
- `Editor/Registry/RegistryStore.cs` — cross-process file lock around the read-modify-write in `Upsert`/`Remove`.
- `Editor/Lifecycle/RecoveryWatchdog.cs` **(NEW)** — background thread: listener re-bind + control-flag + heartbeat.
- `Editor/Lifecycle/ShtlMcpServer.cs` — construct `_http` unconditionally (keep on bind-fail), expose bg-safe recovery entry points, own/stop the watchdog, `volatile` shared fields, port-change hook.
- `Editor/Lifecycle/ShtlMcpBootstrap.cs` — stop the watchdog in `beforeAssemblyReload`; the update-loop `Tick` becomes a thin fallback.
- `Editor/Lifecycle/ShtlMcpConfig.cs` — `IdleKeepAlive` default flips to adaptive-on.
- `Editor/UI/DashboardWindow.cs` — extract a reusable `RunClaudeMcpAdd(...)` callable from the port-change hook.
- Tests: `Tests/Editor/HttpServerAbortTests.cs` (new), `Tests/Editor/RegistryStoreConcurrencyTests.cs` (new), extend `Tests/Editor/ScreenshotToolTests.cs` pattern for config.

---

## Task 1: Immediate port release on reload (P3)

Removes the main trigger that pushes recovery into the background-dependent retry path: `Stop()` leaves the socket in `TIME_WAIT`, so the post-reload `EnsureStarted` bind fails and hands off to the (throttled) watchdog. `Abort()` releases the port synchronously.

**Files:**
- Modify: `Editor/Transport/HttpServer.cs`
- Modify: `Editor/Lifecycle/ShtlMcpServer.cs:194-198` (`StopListenerForReload`)
- Test: `Tests/Editor/HttpServerAbortTests.cs`

**Interfaces:**
- Produces: `HttpServer.Abort()` — stops the accept loop and calls `HttpListener.Abort()` (immediate, no graceful drain).

- [ ] **Step 1: Write the failing test** — `Tests/Editor/HttpServerAbortTests.cs`

```csharp
using System.Net;
using System.Net.Sockets;
using NUnit.Framework;
using Shtl.Mcp.Server;

namespace Shtl.Mcp.Editor.Tests
{
    public class HttpServerAbortTests
    {
        static int FreePort()
        {
            var l = new TcpListener(IPAddress.Loopback, 0);
            l.Start();
            int p = ((IPEndPoint)l.LocalEndpoint).Port;
            l.Stop();
            return p;
        }

        [Test]
        public void Abort_ReleasesPort_ForImmediateRebind()
        {
            int port = FreePort();
            var a = new HttpServer(port, _ => "", null);
            a.Start();
            Assert.IsTrue(a.IsListening, "first bind");
            a.Abort();

            var b = new HttpServer(port, _ => "", null);
            b.Start();
            Assert.IsTrue(b.IsListening, "same port rebinds immediately after Abort");
            b.Abort();
        }
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: EditMode Test Runner → `HttpServerAbortTests.Abort_ReleasesPort_ForImmediateRebind`
Expected: FAIL — `HttpServer` has no `Abort` method (compile error).

- [ ] **Step 3: Add `Abort()` to `HttpServer`** — after `Stop()` (`HttpServer.cs:133`)

```csharp
        /// Немедленно освободить порт: рвёт незавершённые соединения (в отличие от graceful Stop) —
        /// снимает TIME_WAIT, чтобы новый домен после reload биндил тот же порт сразу.
        public void Abort()
        {
            _running = false;
            try
            {
                _listener?.Abort();
                _listener?.Close();
            }
            catch
            {
                // ignored
            }
            _listener = null;
        }
```

- [ ] **Step 4: Use `Abort` on reload** — `ShtlMcpServer.cs:194-198`

```csharp
        public void StopListenerForReload()
        {
            _http?.Abort();
            _http = null;
        }
```

- [ ] **Step 5: Run test to verify it passes**

Run: EditMode Test Runner → `HttpServerAbortTests`
Expected: PASS.

- [ ] **Step 6: Commit**

```bash
git add Editor/Transport/HttpServer.cs Editor/Lifecycle/ShtlMcpServer.cs Tests/Editor/HttpServerAbortTests.cs
git commit -m "feat(lifecycle): Abort listener on reload for immediate port re-bind"
```

---

## Task 2: Cross-process-safe registry writes

Prerequisite for background heartbeat (Task 5): multiple instances writing `registry.json` concurrently must not lose entries. `Upsert` is read-modify-write with no cross-process lock; `File.Replace` is FS-atomic but doesn't stop a logical lost update.

**Files:**
- Modify: `Editor/Registry/RegistryStore.cs`
- Test: `Tests/Editor/RegistryStoreConcurrencyTests.cs`

**Interfaces:**
- `RegistryStore.Upsert(InstanceEntry)` / `Remove(string)` unchanged in signature; internally serialized by a lock file `<registry>.lock`.

- [ ] **Step 1: Write the failing test** — `Tests/Editor/RegistryStoreConcurrencyTests.cs`

```csharp
using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using NUnit.Framework;
using Shtl.Mcp.Registry;

namespace Shtl.Mcp.Editor.Tests
{
    public class RegistryStoreConcurrencyTests
    {
        [Test]
        public void Concurrent_Upsert_KeepsAllInstances()
        {
            string file = Path.Combine(Path.GetTempPath(), "shtl-reg-" + Guid.NewGuid().ToString("N") + ".json");
            try
            {
                var store = new RegistryStore(file);
                int n = 40;
                Parallel.For(0, n, i =>
                {
                    store.Upsert(new InstanceEntry
                    {
                        ProjectPath = "/proj/" + i,
                        ServerName = "unity-" + i,
                        LastHeartbeat = DateTime.UtcNow
                    });
                });
                var paths = store.Read().Select(e => e.ProjectPath).Distinct().ToList();
                Assert.AreEqual(n, paths.Count, "no instance lost under concurrent writes");
            }
            finally
            {
                if (File.Exists(file)) { File.Delete(file); }
            }
        }
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: EditMode Test Runner → `RegistryStoreConcurrencyTests`
Expected: FAIL — fewer than 40 distinct paths (lost updates) intermittently.

- [ ] **Step 3: Serialize Upsert/Remove with a cross-process file lock** — `RegistryStore.cs`

Add a private helper and route both mutators through it. Replace `Upsert` and `Remove`:

```csharp
        public void Upsert(InstanceEntry e) => WithLock(() =>
        {
            var list = Read();
            list.RemoveAll(x => x.ProjectPath == e.ProjectPath);
            list.Add(e);
            WriteAtomic(list);
        });

        public void Remove(string projectPath) => WithLock(() =>
        {
            var list = Read();
            list.RemoveAll(x => x.ProjectPath == projectPath);
            WriteAtomic(list);
        });

        // Межпроцессная сериализация read-modify-write: эксклюзивный lock-файл рядом с реестром. Несколько
        // Unity-инстансов пишут в ОДИН registry.json (фон-heartbeat) → без этого теряются записи (lost update).
        // Ретраим на IOException (чужой держит lock); бросаем работу молча по исчерпании — heartbeat повторится.
        void WithLock(Action mutate)
        {
            var lockPath = _file + ".lock";
            Directory.CreateDirectory(Path.GetDirectoryName(_file));
            for (int attempt = 0; attempt < 50; attempt++)
            {
                FileStream fs = null;
                try
                {
                    fs = new FileStream(lockPath, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
                }
                catch (IOException)
                {
                    System.Threading.Thread.Sleep(5); // чужой инстанс держит lock — короткий backoff
                    continue;
                }
                try
                {
                    mutate();
                    return;
                }
                finally
                {
                    fs.Dispose();
                }
            }
        }
```

- [ ] **Step 4: Run test to verify it passes**

Run: EditMode Test Runner → `RegistryStoreConcurrencyTests`
Expected: PASS — 40 distinct paths.

- [ ] **Step 5: Commit**

```bash
git add Editor/Registry/RegistryStore.cs Tests/Editor/RegistryStoreConcurrencyTests.cs
git commit -m "feat(registry): cross-process file lock so concurrent heartbeats don't lose entries"
```

---

## Task 3: Keep `_http` alive on bind-fail; make shared fields bg-safe

The watchdog re-binds an existing `HttpServer` (router already wired). So `EnsureStarted` must construct `_http` unconditionally and keep it even when the initial bind fails — instead of nulling it (`ShtlMcpServer.cs:180-185`). Fields the watchdog reads must be `volatile`.

**Files:**
- Modify: `Editor/Lifecycle/ShtlMcpServer.cs` (`_http`/`_serverName` fields; `EnsureStarted` tail 178-189)
- Modify: `Editor/Transport/HttpServer.cs` (`IsListening` already null-safe; no change beyond Task 1)

**Interfaces:**
- Produces: `_http` is non-null whenever the server is configured (bound or awaiting re-bind). `IsListening` reflects actual bind state.
- Produces (bg-safe): `bool TryReBindListener()` — returns true if now listening; calls `_http.Start()` only when `_http != null && !_http.IsListening`.

- [ ] **Step 1: Make shared fields volatile** — `ShtlMcpServer.cs:41,43`

```csharp
        volatile HttpServer _http;
        bool _customToolsDiscovered;
        volatile string _serverName;
```

- [ ] **Step 2: Construct `_http` unconditionally; don't null on bind-fail** — replace `ShtlMcpServer.cs:178-188`

```csharp
            _http = new HttpServer(Port, router.Handle, () => _lastRequestUtc = DateTime.UtcNow);
            _http.Start();
            // bind мог не удаться (порт ещё в TIME_WAIT) — _http НЕ обнуляем: объект с готовым router'ом
            // остаётся, фоновый watchdog повторит _http.Start() до успеха (не зависит от update-loop).
            if (_http.IsListening)
            {
                _listenerStartedUtc = DateTime.UtcNow;
                Heartbeat();
            }
            IdleKeepAlive.Reconcile(ShtlMcpConfig.Enabled && ShtlMcpConfig.IdleKeepAlive);
```

- [ ] **Step 3: Add bg-safe re-bind entry point** — new method in `ShtlMcpServer.cs` near `RestartNow`

```csharp
        /// Фоново-безопасный ре-бинд: только HttpListener.Start (без main-thread API). Смену ПОРТА здесь
        /// НЕ делаем — это registry-aware ResolvePort на главном потоке (EnsureStarted). Тут лишь поднимаем
        /// listener на уже выбранном порту. Возвращает текущее состояние прослушивания.
        internal bool TryReBindListener()
        {
            var http = _http;
            if (http == null)
            {
                return false; // ещё не сконструирован (до первого EnsureStarted) — поднимет main
            }
            if (!http.IsListening)
            {
                http.Start();
                if (http.IsListening)
                {
                    _listenerStartedUtc = DateTime.UtcNow;
                }
            }
            return http.IsListening;
        }
```

- [ ] **Step 4: Verify compile** — recompile the package (no new test; behavior covered by Task 4 integration).

Run: EditMode Test Runner (existing suite)
Expected: PASS, 0 compile errors.

- [ ] **Step 5: Commit**

```bash
git add Editor/Lifecycle/ShtlMcpServer.cs
git commit -m "refactor(lifecycle): keep _http on bind-fail + bg-safe TryReBindListener, volatile shared fields"
```

---

## Task 4: Background recovery watchdog (re-bind + control-flag)

The core fix. A dedicated background thread performs listener re-bind and control-flag handling on a fixed cadence, independent of `EditorApplication.update`. This survives window-unfocus throttle and modal blocks: listener/ping/restart stay alive while the main thread is frozen.

**Files:**
- Create: `Editor/Lifecycle/RecoveryWatchdog.cs`
- Modify: `Editor/Lifecycle/ShtlMcpServer.cs` — own the watchdog; expose bg-safe `CheckControlFlagBg()`; start in `EnsureStarted`, stop in `StopListenerForReload`.
- Modify: `Editor/Lifecycle/ShtlMcpBootstrap.cs` — `Tick` becomes a fallback only.

**Interfaces:**
- Consumes: `ShtlMcpServer.TryReBindListener()` (Task 3), a bg-safe control-flag reader, a bg-safe heartbeat (Task 5).
- Produces: `RecoveryWatchdog(Action tick, int intervalMs)` with `Start()`/`Stop()`; runs `tick` off-main every `intervalMs` until stopped.

- [ ] **Step 1: Create the watchdog thread** — `Editor/Lifecycle/RecoveryWatchdog.cs`

```csharp
using System;
using System.Threading;

namespace Shtl.Mcp.Lifecycle
{
    /// Фоновый поток восстановления, НЕЗАВИСИМЫЙ от EditorApplication.update (тот троттлится без фокуса и
    /// замирает на модалке). Держит listener живым (ре-бинд), читает control-flag и пишет heartbeat, пока
    /// главный поток заблокирован. Только bg-safe работа: HttpListener bind + файловый IO, без UnityEngine/*.
    public sealed class RecoveryWatchdog
    {
        readonly Action _tick;
        readonly int _intervalMs;
        Thread _thread;
        volatile bool _running;

        public RecoveryWatchdog(Action tick, int intervalMs)
        {
            _tick = tick;
            _intervalMs = intervalMs;
        }

        public void Start()
        {
            if (_running)
            {
                return;
            }
            _running = true;
            _thread = new Thread(Loop) { IsBackground = true, Name = "ShtlMcpWatchdog" };
            _thread.Start();
        }

        void Loop()
        {
            while (_running)
            {
                try
                {
                    _tick();
                }
                catch
                {
                    // одиночный сбой тика не валит поток восстановления
                }
                Thread.Sleep(_intervalMs);
            }
        }

        public void Stop()
        {
            _running = false;
            _thread = null; // поток IsBackground — сам завершится по _running=false
        }
    }
}
```

- [ ] **Step 2: Add a bg-safe control-flag reader** — `ShtlMcpServer.cs`, alongside `CheckControlFlag`

```csharp
        // Фоновый вариант control-flag: читает+удаляет файл и, при "restart", ре-биндит listener напрямую
        // (Abort+Start) — БЕЗ EnsureStarted, т.к. полный setup требует главного потока. Полный рестарт с
        // ре-регистрацией тулов остаётся за EnsureStarted на главном потоке (следующий Init/afterReload).
        internal void CheckControlFlagBg()
        {
            var name = _serverName;
            if (string.IsNullOrEmpty(name))
            {
                return;
            }
            var cmdPath = System.IO.Path.Combine(System.IO.Path.GetDirectoryName(RegistryPath), name + ".cmd");
            if (!System.IO.File.Exists(cmdPath))
            {
                return;
            }
            string cmd;
            try
            {
                cmd = System.IO.File.ReadAllText(cmdPath).Trim();
                System.IO.File.Delete(cmdPath);
            }
            catch
            {
                return;
            }
            if (cmd == "restart")
            {
                var http = _http;
                http?.Abort();
                http?.Start(); // тот же порт; при неуспехе следующий тик TryReBindListener добьёт
            }
        }
```

- [ ] **Step 3: Own the watchdog in the server** — add field + start/stop

Field near `_http` (`ShtlMcpServer.cs:41`):

```csharp
        RecoveryWatchdog _watchdog;
```

At the end of `EnsureStarted` (after the `IdleKeepAlive.Reconcile` line from Task 3):

```csharp
            if (_watchdog == null)
            {
                _watchdog = new RecoveryWatchdog(WatchdogBgTick, 1000);
                _watchdog.Start();
            }
```

New bg tick aggregator:

```csharp
        // Один тик фонового восстановления (вне главного потока): поднять listener + исполнить restart-флаг.
        // Heartbeat добавляется в Task 5. Порт/имя здесь не меняем — только liveness на уже выбранном порту.
        void WatchdogBgTick()
        {
            TryReBindListener();
            CheckControlFlagBg();
        }
```

In `StopListenerForReload` (extend Task 1 version):

```csharp
        public void StopListenerForReload()
        {
            _watchdog?.Stop();
            _watchdog = null;
            _http?.Abort();
            _http = null;
        }
```

- [ ] **Step 4: Demote the update-loop watchdog to a fallback** — `ShtlMcpBootstrap.cs:56-66`

The main-thread `Tick` still runs main-only chores (job finalize, sweep, keepalive reconcile) but no longer is the *only* path for re-bind/control-flag. Leave `WatchdogTick()` as-is (idempotent) — it now races harmlessly with the bg thread (both guarded). Add a comment:

```csharp
        static void Tick()
        {
            double now = EditorApplication.timeSinceStartup;
            if (now - _lastTick < ShtlMcpConfig.HeartbeatSeconds)
            {
                return;
            }
            _lastTick = now;
            // Главпоточный watchdog остаётся для main-only задач (job finalize, orphan sweep, keepalive).
            // Ре-бинд listener'а и control-flag теперь дублирует фоновый RecoveryWatchdog (оба идемпотентны),
            // поэтому восстановление больше НЕ зависит от того, тикает ли update.
            ShtlMcpServer.Instance.WatchdogTick();
        }
```

- [ ] **Step 5: Integration verification (threading — not unit-testable)**

Manual/integration procedure, run in a live editor:
1. Enter a state where `_http` is bound; from an external shell kill the port by starting Play/edit reload, OR force `_http.Abort()` via `run_csharp` while the Editor window is **unfocused**.
2. Keep the window unfocused (so `EditorApplication.update` is throttled).
3. Within ~1–2s, `ping`/`status` over MCP must succeed — proving the background watchdog re-bound the listener without any focus/update.
4. Write `restart` to `~/.unity-mcp/<serverName>.cmd` with the window unfocused → listener must cycle and stay reachable.

Expected: server reachable throughout without focusing the window or dismissing anything.

- [ ] **Step 6: Commit**

```bash
git add Editor/Lifecycle/RecoveryWatchdog.cs Editor/Lifecycle/ShtlMcpServer.cs Editor/Lifecycle/ShtlMcpBootstrap.cs
git commit -m "feat(lifecycle): background recovery watchdog — re-bind + control-flag off the update loop"
```

---

## Task 5: Move heartbeat onto the watchdog

With Task 2's lock in place, heartbeat can run off-main so `registry.json` `LastHeartbeat` no longer freezes when the main thread is blocked — which is what currently lets a live-but-wedged instance be mistaken for dead (TTL expiry → a same-project instance could steal its serverName/port under `--scope user`).

**Files:**
- Modify: `Editor/Lifecycle/ShtlMcpServer.cs` — split `Heartbeat()` into main-thread state capture vs bg-safe write; call the bg write from `WatchdogBgTick`.

**Interfaces:**
- Consumes: `RegistryStore.Upsert` (now locked, Task 2).
- Produces: `HeartbeatBg()` — reads only bg-safe snapshot fields (no `EditorApplication.*`) and upserts.

- [ ] **Step 1: Capture main-thread-only fields into volatile snapshot**

`Heartbeat()` currently reads main-thread state (`mode`, `Compiling`, pid, etc.). Cache the main-thread-derived values on each `EnsureStarted`/`WatchdogTick` (main) into volatile fields, and have the bg writer use the snapshot. Add fields near `_serverName`:

```csharp
        volatile string _hbMode = "edit";
        volatile bool _hbCompiling;
```

In the existing main-thread `WatchdogTick()` (before its `Heartbeat()` call, `ShtlMcpServer.cs:225`), refresh the snapshot:

```csharp
            _hbMode = EditorApplication.isPlayingOrWillChangePlaymode ? "play" : "edit";
            _hbCompiling = EditorApplication.isCompiling;
```

- [ ] **Step 2: Add bg-safe heartbeat write** — new method; reuse the existing `InstanceEntry` build but from snapshot

```csharp
        // Bg-heartbeat: пишет registry из volatile-снимка (без EditorApplication.*). Держит LastHeartbeat
        // свежим, пока главный поток заблокирован → registry не «хоронит» живой инстанс → его serverName/port
        // не перехватит тёзка того же проекта под --scope user. Registry write сериализован файл-локом (Task 2).
        void HeartbeatBg()
        {
            var name = _serverName;
            var http = _http;
            if (string.IsNullOrEmpty(name) || http == null)
            {
                return;
            }
            _registry.Upsert(new InstanceEntry
            {
                ProjectName = _hbProjectName,
                ProjectPath = _hbProjectPath,
                UnityVersion = _hbUnityVersion,
                ServerName = name,
                Port = Port,
                Pid = _hbPid,
                Mode = _hbMode,
                Compiling = _hbCompiling,
                StartedAt = _hbStartedAt,
                LastHeartbeat = DateTime.UtcNow,
                Recovery = _hbRecovery
            });
        }
```

Add the remaining snapshot fields (captured once in `EnsureStarted`, they're reload-stable): `_hbProjectName`, `_hbProjectPath`, `_hbUnityVersion`, `_hbPid`, `_hbStartedAt`, `_hbRecovery`. Populate them where the current `Heartbeat()` computes them (move those reads to `EnsureStarted`, main thread).

- [ ] **Step 3: Call bg heartbeat from the watchdog tick** — extend `WatchdogBgTick`

```csharp
        void WatchdogBgTick()
        {
            TryReBindListener();
            CheckControlFlagBg();
            HeartbeatBg();
        }
```

Remove the `Heartbeat()` call from main `WatchdogTick` (bg owns it now); keep the main path writing only if the bg thread is absent (defensive: `if (_watchdog == null) Heartbeat();`).

- [ ] **Step 4: Integration verification**

1. Block the main thread (open a modal, or `run_csharp` a `Thread.Sleep(20000)` on main) with the window unfocused.
2. From a shell, read `~/.unity-mcp/registry.json` twice, ~3s apart.
3. `LastHeartbeat` for this instance MUST advance (bg thread keeps writing) even though the main thread is frozen.

Expected: `LastHeartbeat` stays fresh → instance not falsely pruned.

- [ ] **Step 5: Commit**

```bash
git add Editor/Lifecycle/ShtlMcpServer.cs
git commit -m "feat(registry): background heartbeat so a wedged-but-live instance isn't falsely pruned"
```

---

## Task 6: Adaptive IdleKeepAlive default

Cheap mitigation for the unfocused-throttle case so the *main-thread* chores (job finalize, orphan sweep) also keep progressing in the background — without pinning No-Throttling permanently (battery). Default flips from off to adaptive-on: keep full-rate while a client is recently active or a reload just happened; release on deep idle.

**Files:**
- Modify: `Editor/Lifecycle/ShtlMcpConfig.cs:60-64` (default)
- Modify: `Editor/Lifecycle/ShtlMcpServer.cs` — compute `wanted` adaptively in `WatchdogTick`.

**Interfaces:**
- Consumes: `LastRequestAgeSeconds` (exists, `ShtlMcpServer.cs:52`), `ReloadCount`.
- Produces: adaptive `wanted` = `Enabled && IdleKeepAlive && (recentClient || recentReload)`.

- [ ] **Step 1: Flip the config default** — `ShtlMcpConfig.cs`

```csharp
        // Default ON: без него update троттлится в фоне и main-only chores (job finalize/sweep) ползут.
        // Расход батареи ограничен адаптивностью в WatchdogTick (full-rate только при активном клиенте).
        public static bool IdleKeepAlive
        {
            get => EditorPrefs.GetBool(KeepAliveKey, true);
            set => EditorPrefs.SetBool(KeepAliveKey, value);
        }
```

- [ ] **Step 2: Make `wanted` adaptive** — replace the two `IdleKeepAlive.Reconcile(...)` calls in `WatchdogTick` (`ShtlMcpServer.cs:210,228`)

```csharp
            bool recentClient = LastRequestAgeSeconds >= 0 && LastRequestAgeSeconds < 120;
            bool wantKeepAlive = ShtlMcpConfig.Enabled && ShtlMcpConfig.IdleKeepAlive && recentClient;
            IdleKeepAlive.Reconcile(wantKeepAlive);
```

(Apply the same `wantKeepAlive` to both the pre-gate and post-sweep reconcile calls.)

- [ ] **Step 3: Unit-test the adaptive predicate** — extract to a testable pure function

Add to `ShtlMcpServer` (internal, for test visibility via existing `InternalsVisibleTo`):

```csharp
        internal static bool WantKeepAlive(bool enabled, bool toggle, double lastRequestAgeSeconds)
            => enabled && toggle && lastRequestAgeSeconds >= 0 && lastRequestAgeSeconds < 120;
```

Test — `Tests/Editor/KeepAlivePolicyTests.cs`:

```csharp
using NUnit.Framework;
using Shtl.Mcp.Lifecycle;

namespace Shtl.Mcp.Editor.Tests
{
    public class KeepAlivePolicyTests
    {
        [TestCase(true, true, 5.0, true, TestName = "active_client_on")]
        [TestCase(true, true, 300.0, false, TestName = "idle_client_off")]
        [TestCase(true, true, -1.0, false, TestName = "no_client_off")]
        [TestCase(true, false, 5.0, false, TestName = "toggle_off")]
        [TestCase(false, true, 5.0, false, TestName = "server_off")]
        public void WantKeepAlive_Cases(bool en, bool tog, double age, bool exp)
        {
            Assert.AreEqual(exp, ShtlMcpServer.WantKeepAlive(en, tog, age));
        }
    }
}
```

Use `ShtlMcpServer.WantKeepAlive(...)` inside `WatchdogTick` instead of the inline expression.

- [ ] **Step 4: Run tests**

Run: EditMode Test Runner → `KeepAlivePolicyTests`
Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add Editor/Lifecycle/ShtlMcpConfig.cs Editor/Lifecycle/ShtlMcpServer.cs Tests/Editor/KeepAlivePolicyTests.cs
git commit -m "feat(lifecycle): adaptive IdleKeepAlive default-on while a client is active"
```

---

## Task 7: Re-register `--scope user` on port change

Under global (`--scope user`) registration the client dials a fixed `http://127.0.0.1:<port>/mcp`. If `PortAllocator` falls off the deterministic preferred port (occupied), the global entry silently points at the wrong port. Detect a port change vs the registered port and re-run `claude mcp add --scope user` with the new port.

**Files:**
- Modify: `Editor/UI/DashboardWindow.cs` — extract `RunClaudeMcpAdd(serverName, port, done)` from `OnAddClicked`/`McpAddArgs`.
- Modify: `Editor/Lifecycle/ShtlMcpServer.cs` — after `ResolvePort`, if the resolved port differs from the last-registered port (persisted), enqueue a re-registration on the main thread.

**Interfaces:**
- Consumes: existing `RunClaudeAsync` runner (`DashboardWindow.cs:235`).
- Produces: `PortRegistration.LastRegisteredPort` (SessionState/EditorPrefs) and a main-thread `ReRegisterUserScope(name, port)` callable without the Dashboard window being open.

- [ ] **Step 1: Extract a window-independent CLI runner**

Move the `claude` process invocation (`RunClaude`, `ClaudeBin`, arg building) into a small static helper `Editor/UI/ClaudeCli.cs` so it can be called from lifecycle without an open Dashboard:

```csharp
using System;

namespace Shtl.Mcp.Server
{
    /// Запуск `claude` CLI вне Dashboard (переиспользуется lifecycle для ре-регистрации порта).
    public static class ClaudeCli
    {
        public static string AddUserScopeArgs(string serverName, int port)
            => $"mcp add --transport http --scope user {serverName} http://127.0.0.1:{port}/mcp";

        // Реализация RunClaude/ClaudeBin переносится сюда из DashboardWindow (без изменений в логике).
        // Возвращает (exitCode, output). Вызывать с ФОНОВОГО потока (Process.WaitForExit блокирует).
        public static (int, string) Run(string args, int timeoutMs) { /* перенос из DashboardWindow.RunClaude */ return (0, ""); }
    }
}
```

`DashboardWindow` delegates its `McpAddArgs`/`RunClaude` to `ClaudeCli` (no behavior change).

- [ ] **Step 2: Track the registered port and re-register on drift** — `ShtlMcpServer.cs`

Add persisted key + check inside `EnsureStarted`, right after `Port = ResolvePort();` and `_serverName = ...`:

```csharp
            int lastRegistered = SessionState.GetInt(RegisteredPortKey, 0);
            if (lastRegistered != 0 && lastRegistered != Port)
            {
                // Порт сменился (preferred был занят) → глобальная --scope user запись указывает на старый.
                // Пере-регистрируем на фоне (Process блокирующий), чтобы клиент нашёл новый порт.
                var name = _serverName;
                int port = Port;
                System.Threading.Tasks.Task.Run(() =>
                {
                    Shtl.Mcp.Server.ClaudeCli.Run(Shtl.Mcp.Server.ClaudeCli.AddUserScopeArgs(name, port), 8000);
                });
            }
            SessionState.SetInt(RegisteredPortKey, Port);
```

Add the key near the others (`ShtlMcpServer.cs:24`):

```csharp
        const string RegisteredPortKey = "Shtl.Mcp.RegisteredPort";
```

- [ ] **Step 3: Unit-test the drift predicate** — pure function

```csharp
        internal static bool ShouldReRegister(int lastRegistered, int currentPort)
            => lastRegistered != 0 && lastRegistered != currentPort;
```

Test — `Tests/Editor/PortReRegisterTests.cs`:

```csharp
using NUnit.Framework;
using Shtl.Mcp.Lifecycle;

namespace Shtl.Mcp.Editor.Tests
{
    public class PortReRegisterTests
    {
        [TestCase(0, 9750, false, TestName = "first_time_no_prior")]
        [TestCase(9750, 9750, false, TestName = "same_port_noop")]
        [TestCase(9750, 9751, true, TestName = "port_drifted_rereg")]
        public void ShouldReRegister_Cases(int last, int cur, bool exp)
        {
            Assert.AreEqual(exp, ShtlMcpServer.ShouldReRegister(last, cur));
        }
    }
}
```

Use `ShouldReRegister(lastRegistered, Port)` in `EnsureStarted` instead of the inline condition.

- [ ] **Step 4: Run tests**

Run: EditMode Test Runner → `PortReRegisterTests`
Expected: PASS.

- [ ] **Step 5: Integration verification**

1. Occupy this project's preferred port with an external process before Unity starts (or force `PortRangeStart` collision).
2. Start the server → it allocates a different port and re-runs `claude mcp add --scope user` with the new port.
3. `claude mcp list` shows the instance's URL on the new port; a fresh MCP call from any directory reaches it.

Expected: global registration follows the live port automatically.

- [ ] **Step 6: Commit**

```bash
git add Editor/UI/ClaudeCli.cs Editor/UI/DashboardWindow.cs Editor/Lifecycle/ShtlMcpServer.cs Tests/Editor/PortReRegisterTests.cs
git commit -m "feat(lifecycle): re-register --scope user when the allocated port drifts"
```

---

## Self-Review

**Spec coverage** (against the review findings that motivated this plan):
- P1 background watchdog (re-bind + control-flag off the update loop) → Tasks 3, 4.
- P1 background heartbeat (no false prune) → Task 5.
- P2 adaptive IdleKeepAlive → Task 6.
- P3 immediate port release → Task 1.
- Multi-instance: registry multi-writer safety → Task 2; bg re-bind restricted to own port (no bg reallocation) → Task 3/4 (`TryReBindListener` never reselects a port); serverName stability via fresh heartbeat → Task 5.
- Global `--scope user`: port stability + auto re-registration on drift → Tasks 1, 7.

**Placeholder scan:** Task 7 Step 1 `ClaudeCli.Run` body is a move of existing `DashboardWindow.RunClaude` — flagged inline as "перенос из DashboardWindow" rather than re-pasting the process-launch code; the executor moves the existing method verbatim. All other steps carry complete code.

**Type consistency:** `TryReBindListener` (Task 3) consumed by `WatchdogBgTick` (Task 4); `HeartbeatBg` (Task 5) added to the same `WatchdogBgTick`; `WantKeepAlive`/`ShouldReRegister` (Tasks 6/7) are `internal static` reachable via existing `InternalsVisibleTo("Shtl.Mcp.Editor.Tests")` (`Editor/Tools/AssemblyInfo.cs`). Snapshot fields `_hb*` defined in Task 5 are the only new state the bg heartbeat reads.

**Known non-unit-testable surfaces** (integration/manual, called out per task): background re-bind under unfocus (Task 4 Step 5), background heartbeat under a blocked main thread (Task 5 Step 4), port re-registration (Task 7 Step 5). Everything pure (Abort port release, registry locking, keepalive predicate, drift predicate) is unit-tested.

**Ordering:** 1 (Abort) → 2 (registry lock, prereq for 5) → 3 (bg-safe fields) → 4 (watchdog) → 5 (bg heartbeat) → 6 (keepalive) → 7 (re-register). Each task ends at an independently reviewable deliverable.
