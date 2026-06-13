# M1 — Walking Skeleton Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Из Claude Code подключиться к встроенному в Unity MCP-серверу одной командой `claude mcp add`, вызвать `status`/`get_logs`, и доказать, что сервер переживает перекомпиляцию и play/edit-переход.

**Architecture:** MCP-сервер живёт внутри Unity Editor: фоновый `HttpListener` принимает JSON-RPC 2.0, маршалит вызовы инструментов в главный поток через `MainThreadDispatcher` (`EditorApplication.update`), выживает при domain reload через `[InitializeOnLoad]` + `AssemblyReloadEvents` + `SessionState`, и регистрируется в `~/.unity-mcp/registry.json` на детерминированном по пути порту. Подробности — `wiki/systems/`.

**Tech Stack:** C#, Unity 2022.3 LTS, .NET Standard 2.1/Mono, `System.Net.HttpListener` (BCL), Newtonsoft.Json (`com.unity.nuget.newtonsoft-json`), Unity Test Framework (NUnit, EditMode), UI Toolkit.

---

## Соглашения по запуску тестов

EditMode-тесты гоняются через Unity CLI (boot ~30–60 c) или интерактивно в **Window → General → Test Runner → EditMode**. Для CLI задайте путь к редактору и используйте сниппет:

```bash
export UNITY="/Applications/Unity/Hub/Editor/2022.3.62f3/Unity.app/Contents/MacOS/Unity"   # проверено: batchmode+лицензия работают
run_tests() {  # $1 = -testFilter значение (класс или метод)
  "$UNITY" -batchmode -runTests -projectPath "$(pwd)" \
    -testPlatform EditMode -testResults "$(pwd)/TestResults.xml" \
    -testFilter "$1" -logFile - ; echo "exit=$?"
}
```
`-runTests` сам завершает редактор. Результат — в консоли (`-logFile -`) и `TestResults.xml` (`exit=0` и `result="Passed"` = PASS; `exit=2` = провал тестов).

## File Structure

Dev-проект Unity = корень репо. Пакет — **embedded UPM** в `Packages/com.shtl.mcp/` (самостоятельно дистрибутируемый и dev-тестируемый).

```
Packages/com.shtl.mcp/
  package.json
  Editor/
    ShtlMcp.Editor.asmdef          # один Editor-asmdef на M1 (логический split по папкам)
    Common/Fnv.cs                  # детерминированный хеш пути
    Common/PortAllocator.cs        # порт = base + hash(path) % range, fallback
    Common/ServerName.cs           # unity-<product>, дедуп по hash4(path)
    Registry/InstanceEntry.cs      # запись реестра
    Registry/RegistryStore.cs      # чтение/atomic-запись/prune ~/.unity-mcp/registry.json
    Dispatch/MainThreadDispatcher.cs
    Logging/LogLevel.cs
    Logging/LogBuffer.cs           # потокобезопасный ring-buffer логов
    Transport/JsonRpc.cs           # типы + парс/сериализация JSON-RPC 2.0
    Transport/IToolInvoker.cs
    Transport/McpRouter.cs         # initialize/tools.list/tools.call (чистый string→string)
    Tools/ITool.cs
    Tools/IEditorContext.cs
    Tools/ToolRegistry.cs
    Tools/StatusTool.cs
    Tools/GetLogsTool.cs
    Server/HttpServer.cs           # HttpListener + фоновый цикл
    Server/DispatchingToolInvoker.cs
    Lifecycle/ShtlMcpConfig.cs     # EditorPrefs (enabled)
    Lifecycle/ShtlMcpServer.cs     # фасад: порт, http, dispatcher, registry, logs, uptime
    Lifecycle/ShtlMcpBootstrap.cs  # [InitializeOnLoad] + reload-события + watchdog
    Lifecycle/EditorContext.cs     # IEditorContext поверх UnityEditor API
    UI/DashboardWindow.cs          # единственное окно (UI Toolkit)
  Tests/Editor/
    ShtlMcp.Editor.Tests.asmdef
    FnvTests.cs PortAllocatorTests.cs ServerNameTests.cs RegistryStoreTests.cs
    MainThreadDispatcherTests.cs LogBufferTests.cs JsonRpcTests.cs McpRouterTests.cs
    StatusToolTests.cs GetLogsToolTests.cs
```

> **Deviation note (записать в journal):** `wiki/systems/architecture.md` задаёт 6 раздельных asmdef. M1 использует один Editor-asmdef с папками-модулями; физический split откладываем в M2, когда границы подтверждены кодом. Это деталь реализации, инварианты не затронуты.

---

### Task 1: Bootstrap Unity dev-проекта и скелета пакета

**Files:**
- Create: `ProjectSettings/*` (генерится Unity), `Packages/manifest.json`, `Assets/.gitkeep`
- Create: `Packages/com.shtl.mcp/package.json`
- Create: `Packages/com.shtl.mcp/Editor/ShtlMcp.Editor.asmdef`
- Create: `Packages/com.shtl.mcp/Tests/Editor/ShtlMcp.Editor.Tests.asmdef`

- [ ] **Step 1: Создать Unity-проект в корне репо**

```bash
"$UNITY" -batchmode -createProject "$(pwd)" -quit -logFile -
```
Создаст `Assets/`, `Packages/manifest.json`, `ProjectSettings/` рядом с существующими `raw/`, `wiki/`, `CLAUDE.md`.

- [ ] **Step 2: Добавить Newtonsoft в манифест**

В `Packages/manifest.json` в `"dependencies"` добавить строку:
```json
"com.unity.nuget.newtonsoft-json": "3.2.1",
```

- [ ] **Step 3: Создать `package.json` пакета**

`Packages/com.shtl.mcp/package.json`:
```json
{
  "name": "com.shtl.mcp",
  "version": "0.1.0",
  "displayName": "Shtl MCP",
  "description": "Self-contained in-Unity MCP server.",
  "unity": "2022.3",
  "dependencies": { "com.unity.nuget.newtonsoft-json": "3.2.1" }
}
```

- [ ] **Step 4: Создать asmdef кода**

`Packages/com.shtl.mcp/Editor/ShtlMcp.Editor.asmdef`:
```json
{
  "name": "ShtlMcp.Editor",
  "rootNamespace": "ShtlMcp",
  "references": ["Newtonsoft.Json"],
  "includePlatforms": ["Editor"],
  "autoReferenced": true,
  "noEngineReferences": false
}
```

- [ ] **Step 5: Создать asmdef тестов**

`Packages/com.shtl.mcp/Tests/Editor/ShtlMcp.Editor.Tests.asmdef`:
```json
{
  "name": "ShtlMcp.Editor.Tests",
  "references": ["ShtlMcp.Editor", "Newtonsoft.Json", "UnityEngine.TestRunner", "UnityEditor.TestRunner"],
  "includePlatforms": ["Editor"],
  "optionalUnityReferences": ["TestAssemblies"],
  "precompiledReferences": ["nunit.framework.dll"],
  "defineConstraints": ["UNITY_INCLUDE_TESTS"],
  "autoReferenced": false
}
```

- [ ] **Step 6: Проверить, что проект открывается и компилируется**

```bash
"$UNITY" -batchmode -quit -projectPath "$(pwd)" -logFile - | grep -iE "error CS|Compilation failed" && echo "COMPILE ERRORS" || echo "OK: no compile errors"
```
Expected: `OK: no compile errors`.

- [ ] **Step 7: Commit**

```bash
git add Packages/ ProjectSettings/ Assets/.gitkeep
git commit -m "chore(m1): unity dev project + embedded package skeleton"
```

---

### Task 2: FNV-1a хеш пути

**Files:**
- Create: `Packages/com.shtl.mcp/Editor/Common/Fnv.cs`
- Test: `Packages/com.shtl.mcp/Tests/Editor/FnvTests.cs`

- [ ] **Step 1: Написать падающий тест**

`Tests/Editor/FnvTests.cs`:
```csharp
using NUnit.Framework;
using ShtlMcp.Common;

namespace ShtlMcp.Editor.Tests
{
    public class FnvTests
    {
        [Test] public void Hash32_IsDeterministic()
            => Assert.AreEqual(Fnv.Hash32("/Users/a/PerfectWar"), Fnv.Hash32("/Users/a/PerfectWar"));

        [Test] public void Hash32_DiffersForDifferentInput()
            => Assert.AreNotEqual(Fnv.Hash32("/a"), Fnv.Hash32("/b"));

        [Test] public void Hash4_HasFourHexChars()
        {
            var h = Fnv.Hash4("/Users/a/PerfectWar");
            Assert.AreEqual(4, h.Length);
            StringAssert.IsMatch("^[0-9a-f]{4}$", h);
        }
    }
}
```

- [ ] **Step 2: Запустить тест — убедиться, что падает**

Run: `run_tests "ShtlMcp.Editor.Tests.FnvTests"`
Expected: FAIL — компиляция падает (`Fnv` не существует).

- [ ] **Step 3: Реализация**

`Editor/Common/Fnv.cs`:
```csharp
namespace ShtlMcp.Common
{
    /// FNV-1a по байтам UTF-16 — детерминирован между процессами (в отличие от string.GetHashCode).
    public static class Fnv
    {
        public static uint Hash32(string s)
        {
            const uint offset = 2166136261u, prime = 16777619u;
            uint h = offset;
            foreach (char c in s)
            {
                h ^= (byte)(c & 0xFF);       h *= prime;
                h ^= (byte)((c >> 8) & 0xFF); h *= prime;
            }
            return h;
        }

        public static string Hash4(string s) => (Hash32(s) & 0xFFFF).ToString("x4");
    }
}
```

- [ ] **Step 4: Запустить тест — PASS**

Run: `run_tests "ShtlMcp.Editor.Tests.FnvTests"`
Expected: PASS (`exit=0`).

- [ ] **Step 5: Commit**

```bash
git add Packages/com.shtl.mcp/Editor/Common/Fnv.cs Packages/com.shtl.mcp/Tests/Editor/FnvTests.cs
git commit -m "feat(m1): deterministic FNV-1a path hash"
```

---

### Task 3: PortAllocator

**Files:**
- Create: `Packages/com.shtl.mcp/Editor/Common/PortAllocator.cs`
- Test: `Packages/com.shtl.mcp/Tests/Editor/PortAllocatorTests.cs`

- [ ] **Step 1: Написать падающий тест**

```csharp
using System;
using NUnit.Framework;
using ShtlMcp.Common;

namespace ShtlMcp.Editor.Tests
{
    public class PortAllocatorTests
    {
        [Test] public void Preferred_IsDeterministicAndInRange()
        {
            int p = PortAllocator.Preferred("/Users/a/PerfectWar");
            Assert.AreEqual(p, PortAllocator.Preferred("/Users/a/PerfectWar"));
            Assert.GreaterOrEqual(p, PortAllocator.Base);
            Assert.Less(p, PortAllocator.Base + PortAllocator.Range);
        }

        [Test] public void Allocate_ReturnsPreferred_WhenFree()
        {
            int pref = PortAllocator.Preferred("/p");
            Assert.AreEqual(pref, PortAllocator.Allocate("/p", _ => true));
        }

        [Test] public void Allocate_FallsBack_WhenPreferredTaken()
        {
            int pref = PortAllocator.Preferred("/p");
            int got = PortAllocator.Allocate("/p", port => port != pref);
            Assert.AreNotEqual(pref, got);
            Assert.GreaterOrEqual(got, PortAllocator.Base);
            Assert.Less(got, PortAllocator.Base + PortAllocator.Range);
        }
    }
}
```

- [ ] **Step 2: Запустить — FAIL** (`PortAllocator` не существует).
Run: `run_tests "ShtlMcp.Editor.Tests.PortAllocatorTests"`

- [ ] **Step 3: Реализация**

`Editor/Common/PortAllocator.cs`:
```csharp
using System;

namespace ShtlMcp.Common
{
    public static class PortAllocator
    {
        public const int Base = 9700;
        public const int Range = 100;

        public static int Preferred(string projectPath)
            => Base + (int)(Fnv.Hash32(projectPath) % (uint)Range);

        /// Пробуем preferred, затем по кругу диапазона до первого свободного.
        public static int Allocate(string projectPath, Func<int, bool> isFree)
        {
            int start = Preferred(projectPath);
            for (int i = 0; i < Range; i++)
            {
                int port = Base + (((start - Base) + i) % Range);
                if (isFree(port)) return port;
            }
            throw new InvalidOperationException("No free port in range");
        }
    }
}
```

- [ ] **Step 4: Запустить — PASS.** Run: `run_tests "ShtlMcp.Editor.Tests.PortAllocatorTests"`

- [ ] **Step 5: Commit**

```bash
git add Packages/com.shtl.mcp/Editor/Common/PortAllocator.cs Packages/com.shtl.mcp/Tests/Editor/PortAllocatorTests.cs
git commit -m "feat(m1): deterministic port allocator with collision fallback"
```

---

### Task 4: ServerName (дедуп по пути)

**Files:**
- Create: `Packages/com.shtl.mcp/Editor/Common/ServerName.cs`
- Test: `Packages/com.shtl.mcp/Tests/Editor/ServerNameTests.cs`

- [ ] **Step 1: Падающий тест**

```csharp
using NUnit.Framework;
using ShtlMcp.Common;

namespace ShtlMcp.Editor.Tests
{
    public class ServerNameTests
    {
        [Test] public void Base_WhenNoCollision()
            => Assert.AreEqual("unity-perfectwar",
                 ServerName.Resolve("PerfectWar", "/p", _ => null));

        [Test] public void Base_WhenSamePathAlreadyLive()
            => Assert.AreEqual("unity-perfectwar",
                 ServerName.Resolve("PerfectWar", "/p", _ => "/p"));

        [Test] public void Suffixed_WhenNameCollidesWithDifferentPath()
        {
            string name = ServerName.Resolve("PerfectWar", "/worktree", _ => "/main");
            StringAssert.StartsWith("unity-perfectwar-", name);
            Assert.AreEqual("unity-perfectwar-" + Fnv.Hash4("/worktree"), name);
        }

        [Test] public void Sanitize_LowercasesAndDashes()
            => Assert.AreEqual("unity-my-game", ServerName.Resolve("My Game!", "/p", _ => null));
    }
}
```

- [ ] **Step 2: FAIL.** Run: `run_tests "ShtlMcp.Editor.Tests.ServerNameTests"`

- [ ] **Step 3: Реализация**

`Editor/Common/ServerName.cs`:
```csharp
using System;
using System.Linq;

namespace ShtlMcp.Common
{
    public static class ServerName
    {
        public static string Sanitize(string productName)
        {
            var chars = (productName ?? "")
                .Select(c => char.IsLetterOrDigit(c) ? char.ToLowerInvariant(c) : '-');
            string s = new string(chars.ToArray());
            while (s.Contains("--")) s = s.Replace("--", "-");
            return s.Trim('-');
        }

        /// livePathForName(name) → путь живого инстанса с таким serverName, или null.
        public static string Resolve(string productName, string projectPath, Func<string, string> livePathForName)
        {
            string baseName = "unity-" + Sanitize(productName);
            string existing = livePathForName(baseName);
            if (existing == null || existing == projectPath) return baseName;
            return baseName + "-" + Fnv.Hash4(projectPath);
        }
    }
}
```

- [ ] **Step 4: PASS.** Run: `run_tests "ShtlMcp.Editor.Tests.ServerNameTests"`

- [ ] **Step 5: Commit**

```bash
git add Packages/com.shtl.mcp/Editor/Common/ServerName.cs Packages/com.shtl.mcp/Tests/Editor/ServerNameTests.cs
git commit -m "feat(m1): server name resolver with path-hash dedup"
```

---

### Task 5: Registry (модель + atomic-store + prune)

**Files:**
- Create: `Packages/com.shtl.mcp/Editor/Registry/InstanceEntry.cs`
- Create: `Packages/com.shtl.mcp/Editor/Registry/RegistryStore.cs`
- Test: `Packages/com.shtl.mcp/Tests/Editor/RegistryStoreTests.cs`

- [ ] **Step 1: Падающий тест** (реальная файловая система — temp-каталог)

```csharp
using System;
using System.IO;
using System.Collections.Generic;
using NUnit.Framework;
using ShtlMcp.Registry;

namespace ShtlMcp.Editor.Tests
{
    public class RegistryStoreTests
    {
        string _file;
        [SetUp] public void Setup()
            => _file = Path.Combine(Path.GetTempPath(), "shtlmcp-test-" + Guid.NewGuid().ToString("N"), "registry.json");
        [TearDown] public void Cleanup()
        { try { Directory.Delete(Path.GetDirectoryName(_file), true); } catch { } }

        InstanceEntry Entry(string path, string name, DateTime hb) => new InstanceEntry
        {
            ProjectName = name, ProjectPath = path, UnityVersion = "2022.3",
            ServerName = "unity-" + name, Port = 9712, Pid = 1, Mode = "edit",
            Compiling = false, StartedAt = hb, LastHeartbeat = hb
        };

        [Test] public void Upsert_RoundTrips()
        {
            var store = new RegistryStore(_file);
            store.Upsert(Entry("/p", "perfectwar", DateTime.UtcNow));
            var list = store.Read();
            Assert.AreEqual(1, list.Count);
            Assert.AreEqual("/p", list[0].ProjectPath);
            Assert.AreEqual(9712, list[0].Port);
        }

        [Test] public void Upsert_ReplacesSamePath()
        {
            var store = new RegistryStore(_file);
            store.Upsert(Entry("/p", "a", DateTime.UtcNow));
            store.Upsert(Entry("/p", "a", DateTime.UtcNow)); // повтор того же пути
            Assert.AreEqual(1, store.Read().Count);
        }

        [Test] public void Prune_DropsStale()
        {
            var now = DateTime.UtcNow;
            var list = new List<InstanceEntry>
            {
                Entry("/live", "a", now),
                Entry("/stale", "b", now - TimeSpan.FromMinutes(5)),
            };
            var kept = RegistryStore.Prune(list, now, TimeSpan.FromSeconds(30));
            Assert.AreEqual(1, kept.Count);
            Assert.AreEqual("/live", kept[0].ProjectPath);
        }

        [Test] public void LivePathForName_ReturnsFreshMatchOnly()
        {
            var store = new RegistryStore(_file);
            store.Upsert(Entry("/main", "perfectwar", DateTime.UtcNow));
            Assert.AreEqual("/main", store.LivePathForName("unity-perfectwar", TimeSpan.FromSeconds(30)));
            Assert.IsNull(store.LivePathForName("unity-other", TimeSpan.FromSeconds(30)));
        }
    }
}
```

- [ ] **Step 2: FAIL.** Run: `run_tests "ShtlMcp.Editor.Tests.RegistryStoreTests"`

- [ ] **Step 3: Реализация модели**

`Editor/Registry/InstanceEntry.cs`:
```csharp
using System;

namespace ShtlMcp.Registry
{
    public sealed class InstanceEntry
    {
        public string ProjectName;
        public string ProjectPath;
        public string UnityVersion;
        public string ServerName;
        public int Port;
        public int Pid;
        public string Mode;          // "edit" | "play"
        public bool Compiling;
        public DateTime StartedAt;
        public DateTime LastHeartbeat;
    }
}
```

- [ ] **Step 4: Реализация store**

`Editor/Registry/RegistryStore.cs`:
```csharp
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;

namespace ShtlMcp.Registry
{
    public sealed class RegistryStore
    {
        readonly string _file;
        static readonly JsonSerializerSettings S = new JsonSerializerSettings
        {
            ContractResolver = new CamelCasePropertyNamesContractResolver(),
            Formatting = Formatting.Indented,
            DateTimeZoneHandling = DateTimeZoneHandling.Utc
        };

        public RegistryStore(string file) => _file = file;

        public List<InstanceEntry> Read()
        {
            try
            {
                if (!File.Exists(_file)) return new List<InstanceEntry>();
                var json = File.ReadAllText(_file);
                return JsonConvert.DeserializeObject<List<InstanceEntry>>(json, S) ?? new List<InstanceEntry>();
            }
            catch { return new List<InstanceEntry>(); }
        }

        public void Upsert(InstanceEntry e)
        {
            var list = Read();
            list.RemoveAll(x => x.ProjectPath == e.ProjectPath);
            list.Add(e);
            WriteAtomic(list);
        }

        public void Remove(string projectPath)
        {
            var list = Read();
            list.RemoveAll(x => x.ProjectPath == projectPath);
            WriteAtomic(list);
        }

        public string LivePathForName(string serverName, TimeSpan ttl)
        {
            var now = DateTime.UtcNow;
            return Read().FirstOrDefault(x =>
                x.ServerName == serverName && now - x.LastHeartbeat <= ttl)?.ProjectPath;
        }

        public static List<InstanceEntry> Prune(List<InstanceEntry> list, DateTime now, TimeSpan ttl)
            => list.Where(x => now - x.LastHeartbeat <= ttl).ToList();

        void WriteAtomic(List<InstanceEntry> list)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_file));
            var tmp = _file + ".tmp";
            File.WriteAllText(tmp, JsonConvert.SerializeObject(list, S));
            if (File.Exists(_file)) File.Replace(tmp, _file, null);
            else File.Move(tmp, _file);
        }
    }
}
```

- [ ] **Step 5: PASS.** Run: `run_tests "ShtlMcp.Editor.Tests.RegistryStoreTests"`

- [ ] **Step 6: Commit**

```bash
git add Packages/com.shtl.mcp/Editor/Registry Packages/com.shtl.mcp/Tests/Editor/RegistryStoreTests.cs
git commit -m "feat(m1): instance registry with atomic write, prune, live-name lookup"
```

---

### Task 6: MainThreadDispatcher

**Files:**
- Create: `Packages/com.shtl.mcp/Editor/Dispatch/MainThreadDispatcher.cs`
- Test: `Packages/com.shtl.mcp/Tests/Editor/MainThreadDispatcherTests.cs`

- [ ] **Step 1: Падающий тест**

```csharp
using System;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;
using NUnit.Framework;
using ShtlMcp.Dispatch;

namespace ShtlMcp.Editor.Tests
{
    public class MainThreadDispatcherTests
    {
        [Test] public void Drain_ExecutesFifo()
        {
            var d = new MainThreadDispatcher();
            var log = new List<int>();
            d.Enqueue(() => log.Add(1));
            d.Enqueue(() => log.Add(2));
            d.Drain();
            CollectionAssert.AreEqual(new[] { 1, 2 }, log);
        }

        [Test] public void RunOnMain_ReturnsResult_WhenDrained()
        {
            var d = new MainThreadDispatcher();
            int result = 0;
            var t = Task.Run(() => result = d.RunOnMain(() => 42, 2000));
            while (!t.IsCompleted) { d.Drain(); Thread.Sleep(1); }
            d.Drain(); t.Wait();
            Assert.AreEqual(42, result);
        }

        [Test] public void RunOnMain_Throws_WhenNotDrained()
        {
            var d = new MainThreadDispatcher();
            Assert.Throws<TimeoutException>(() => d.RunOnMain(() => 1, 50));
        }

        [Test] public void RunOnMain_PropagatesException()
        {
            var d = new MainThreadDispatcher();
            var t = Task.Run(() => Assert.Throws<InvalidOperationException>(
                () => d.RunOnMain<int>(() => throw new InvalidOperationException("boom"), 2000)));
            while (!t.IsCompleted) { d.Drain(); Thread.Sleep(1); }
            t.Wait();
        }
    }
}
```

- [ ] **Step 2: FAIL.** Run: `run_tests "ShtlMcp.Editor.Tests.MainThreadDispatcherTests"`

- [ ] **Step 3: Реализация**

`Editor/Dispatch/MainThreadDispatcher.cs`:
```csharp
using System;
using System.Collections.Concurrent;
using System.Threading;

namespace ShtlMcp.Dispatch
{
    /// Фоновый поток кладёт работу, главный поток Unity вызывает Drain() в EditorApplication.update.
    public sealed class MainThreadDispatcher
    {
        readonly ConcurrentQueue<Action> _queue = new ConcurrentQueue<Action>();

        public void Enqueue(Action action) => _queue.Enqueue(action);

        public void Drain()
        {
            while (_queue.TryDequeue(out var a))
            {
                try { a(); } catch { /* изоляция: одна упавшая работа не валит pump */ }
            }
        }

        public T RunOnMain<T>(Func<T> func, int timeoutMs)
        {
            using (var done = new ManualResetEventSlim(false))
            {
                T result = default;
                Exception error = null;
                Enqueue(() =>
                {
                    try { result = func(); }
                    catch (Exception e) { error = e; }
                    finally { done.Set(); }
                });
                if (!done.Wait(timeoutMs))
                    throw new TimeoutException("Main thread did not drain within timeout (compiling?)");
                if (error != null) throw error;
                return result;
            }
        }
    }
}
```
> Примечание: в `Drain()` try/catch проглатывает исключения работ, поставленных через `Enqueue`. Работы из `RunOnMain` оборачивают свой `func` в собственный try/catch ДО общего — поэтому их исключения корректно прокидываются вызывающему потоку (тест `RunOnMain_PropagatesException`).

- [ ] **Step 4: PASS.** Run: `run_tests "ShtlMcp.Editor.Tests.MainThreadDispatcherTests"`

- [ ] **Step 5: Commit**

```bash
git add Packages/com.shtl.mcp/Editor/Dispatch Packages/com.shtl.mcp/Tests/Editor/MainThreadDispatcherTests.cs
git commit -m "feat(m1): main-thread dispatcher with RunOnMain marshaling"
```

---

### Task 7: LogBuffer

**Files:**
- Create: `Packages/com.shtl.mcp/Editor/Logging/LogLevel.cs`
- Create: `Packages/com.shtl.mcp/Editor/Logging/LogBuffer.cs`
- Test: `Packages/com.shtl.mcp/Tests/Editor/LogBufferTests.cs`

- [ ] **Step 1: Падающий тест**

```csharp
using System.Linq;
using NUnit.Framework;
using ShtlMcp.Logging;

namespace ShtlMcp.Editor.Tests
{
    public class LogBufferTests
    {
        [Test] public void Add_EvictsBeyondCapacity()
        {
            var b = new LogBuffer(2);
            b.Add("a", "", LogLevel.Info);
            b.Add("b", "", LogLevel.Info);
            b.Add("c", "", LogLevel.Info);
            var items = b.Get(null, 10);
            Assert.AreEqual(2, items.Count);
            CollectionAssert.AreEqual(new[] { "b", "c" }, items.Select(i => i.Message).ToArray());
        }

        [Test] public void Get_FiltersByMinLevel()
        {
            var b = new LogBuffer(10);
            b.Add("i", "", LogLevel.Info);
            b.Add("w", "", LogLevel.Warning);
            b.Add("e", "", LogLevel.Error);
            var errs = b.Get(LogLevel.Warning, 10).Select(i => i.Message).ToArray();
            CollectionAssert.AreEqual(new[] { "w", "e" }, errs);
        }

        [Test] public void Get_LimitsCount_ReturningMostRecent()
        {
            var b = new LogBuffer(10);
            b.Add("1", "", LogLevel.Info);
            b.Add("2", "", LogLevel.Info);
            b.Add("3", "", LogLevel.Info);
            CollectionAssert.AreEqual(new[] { "2", "3" }, b.Get(null, 2).Select(i => i.Message).ToArray());
        }
    }
}
```

- [ ] **Step 2: FAIL.** Run: `run_tests "ShtlMcp.Editor.Tests.LogBufferTests"`

- [ ] **Step 3: Реализация**

`Editor/Logging/LogLevel.cs`:
```csharp
namespace ShtlMcp.Logging
{
    public enum LogLevel { Info = 0, Warning = 1, Error = 2 }

    public struct LogItem
    {
        public string Message;
        public string Stack;
        public LogLevel Level;
        public LogItem(string message, string stack, LogLevel level)
        { Message = message; Stack = stack; Level = level; }
    }
}
```

`Editor/Logging/LogBuffer.cs`:
```csharp
using System.Collections.Generic;
using System.Linq;

namespace ShtlMcp.Logging
{
    public sealed class LogBuffer
    {
        readonly int _cap;
        readonly LinkedList<LogItem> _items = new LinkedList<LogItem>();
        readonly object _lock = new object();

        public LogBuffer(int capacity) => _cap = capacity;

        public void Add(string message, string stack, LogLevel level)
        {
            lock (_lock)
            {
                _items.AddLast(new LogItem(message, stack, level));
                while (_items.Count > _cap) _items.RemoveFirst();
            }
        }

        /// Возвращает до count последних записей (в хронологическом порядке), c фильтром по min-уровню.
        public IReadOnlyList<LogItem> Get(LogLevel? min, int count)
        {
            lock (_lock)
            {
                IEnumerable<LogItem> q = _items;
                if (min.HasValue) q = q.Where(i => i.Level >= min.Value);
                var all = q.ToList();
                int skip = all.Count > count ? all.Count - count : 0;
                return all.Skip(skip).ToList();
            }
        }
    }
}
```

- [ ] **Step 4: PASS.** Run: `run_tests "ShtlMcp.Editor.Tests.LogBufferTests"`

- [ ] **Step 5: Commit**

```bash
git add Packages/com.shtl.mcp/Editor/Logging Packages/com.shtl.mcp/Tests/Editor/LogBufferTests.cs
git commit -m "feat(m1): thread-safe ring-buffer log capture"
```

---

### Task 8: JSON-RPC типы + McpRouter

**Files:**
- Create: `Packages/com.shtl.mcp/Editor/Transport/JsonRpc.cs`
- Create: `Packages/com.shtl.mcp/Editor/Transport/IToolInvoker.cs`
- Create: `Packages/com.shtl.mcp/Editor/Transport/McpRouter.cs`
- Test: `Packages/com.shtl.mcp/Tests/Editor/JsonRpcTests.cs`
- Test: `Packages/com.shtl.mcp/Tests/Editor/McpRouterTests.cs`

- [ ] **Step 1: Падающие тесты**

`Tests/Editor/JsonRpcTests.cs`:
```csharp
using NUnit.Framework;
using Newtonsoft.Json.Linq;
using ShtlMcp.Transport;

namespace ShtlMcp.Editor.Tests
{
    public class JsonRpcTests
    {
        [Test] public void Error_BuildsStandardEnvelope()
        {
            string s = JsonRpc.Error(7, -32601, "Method not found");
            var o = JObject.Parse(s);
            Assert.AreEqual("2.0", (string)o["jsonrpc"]);
            Assert.AreEqual(7, (int)o["id"]);
            Assert.AreEqual(-32601, (int)o["error"]["code"]);
        }

        [Test] public void Result_BuildsEnvelope()
        {
            string s = JsonRpc.Result(1, new JObject { ["ok"] = true });
            var o = JObject.Parse(s);
            Assert.AreEqual(1, (int)o["id"]);
            Assert.IsTrue((bool)o["result"]["ok"]);
        }
    }
}
```

`Tests/Editor/McpRouterTests.cs`:
```csharp
using NUnit.Framework;
using Newtonsoft.Json.Linq;
using ShtlMcp.Transport;

namespace ShtlMcp.Editor.Tests
{
    class FakeInvoker : IToolInvoker
    {
        public JArray Tools = new JArray {
            new JObject { ["name"] = "status", ["description"] = "x", ["inputSchema"] = new JObject { ["type"] = "object" } }
        };
        public JArray ListTools() => Tools;
        public JObject Invoke(string name, JObject args)
            => name == "status" ? new JObject { ["projectName"] = "PW" }
                                : throw new System.Exception("unknown tool: " + name);
    }

    public class McpRouterTests
    {
        McpRouter NewRouter() => new McpRouter(new FakeInvoker(),
            new ServerInfo { Name = "unity-pw", Version = "0.1.0", Instructions = "hi" });

        [Test] public void Initialize_ReturnsServerInfoAndCapabilities()
        {
            var o = JObject.Parse(NewRouter().Handle(
                @"{""jsonrpc"":""2.0"",""id"":1,""method"":""initialize"",""params"":{}}"));
            Assert.AreEqual("unity-pw", (string)o["result"]["serverInfo"]["name"]);
            Assert.IsNotNull(o["result"]["capabilities"]["tools"]);
            Assert.AreEqual("hi", (string)o["result"]["instructions"]);
        }

        [Test] public void InitializedNotification_ReturnsEmpty()
        {
            string r = NewRouter().Handle(@"{""jsonrpc"":""2.0"",""method"":""notifications/initialized""}");
            Assert.IsTrue(string.IsNullOrEmpty(r));
        }

        [Test] public void ToolsList_ReturnsRegisteredTools()
        {
            var o = JObject.Parse(NewRouter().Handle(
                @"{""jsonrpc"":""2.0"",""id"":2,""method"":""tools/list"",""params"":{}}"));
            Assert.AreEqual("status", (string)o["result"]["tools"][0]["name"]);
        }

        [Test] public void ToolsCall_WrapsResultAsTextContent()
        {
            var o = JObject.Parse(NewRouter().Handle(
                @"{""jsonrpc"":""2.0"",""id"":3,""method"":""tools/call"",""params"":{""name"":""status"",""arguments"":{}}}"));
            Assert.IsFalse((bool)o["result"]["isError"]);
            Assert.AreEqual("text", (string)o["result"]["content"][0]["type"]);
            StringAssert.Contains("PW", (string)o["result"]["content"][0]["text"]);
        }

        [Test] public void UnknownMethod_Returns_MethodNotFound()
        {
            var o = JObject.Parse(NewRouter().Handle(
                @"{""jsonrpc"":""2.0"",""id"":4,""method"":""bogus""}"));
            Assert.AreEqual(-32601, (int)o["error"]["code"]);
        }

        [Test] public void ParseError_Returns_ParseError()
        {
            var o = JObject.Parse(NewRouter().Handle("{ this is not json"));
            Assert.AreEqual(-32700, (int)o["error"]["code"]);
        }

        [Test] public void ToolThrows_Returns_IsErrorContent()
        {
            var o = JObject.Parse(NewRouter().Handle(
                @"{""jsonrpc"":""2.0"",""id"":5,""method"":""tools/call"",""params"":{""name"":""nope"",""arguments"":{}}}"));
            Assert.IsTrue((bool)o["result"]["isError"]);
        }
    }
}
```

- [ ] **Step 2: FAIL.** Run: `run_tests "ShtlMcp.Editor.Tests.JsonRpcTests"` и `run_tests "ShtlMcp.Editor.Tests.McpRouterTests"`

- [ ] **Step 3: Реализация типов**

`Editor/Transport/JsonRpc.cs`:
```csharp
using Newtonsoft.Json.Linq;

namespace ShtlMcp.Transport
{
    public sealed class ServerInfo
    {
        public string Name;
        public string Version;
        public string Instructions;
    }

    public static class JsonRpc
    {
        public static string Result(JToken id, JToken result)
            => new JObject { ["jsonrpc"] = "2.0", ["id"] = id, ["result"] = result }.ToString();

        public static string Error(JToken id, int code, string message)
            => new JObject
            {
                ["jsonrpc"] = "2.0",
                ["id"] = id ?? JValue.CreateNull(),
                ["error"] = new JObject { ["code"] = code, ["message"] = message }
            }.ToString();
    }
}
```

`Editor/Transport/IToolInvoker.cs`:
```csharp
using Newtonsoft.Json.Linq;

namespace ShtlMcp.Transport
{
    public interface IToolInvoker
    {
        JArray ListTools();                       // [{name, description, inputSchema}]
        JObject Invoke(string name, JObject args); // результат инструмента (или бросает)
    }
}
```

- [ ] **Step 4: Реализация роутера**

`Editor/Transport/McpRouter.cs`:
```csharp
using Newtonsoft.Json.Linq;

namespace ShtlMcp.Transport
{
    /// Чистый string→string. MCP минимум: initialize, notifications/initialized, tools/list, tools/call.
    public sealed class McpRouter
    {
        const string ProtocolVersion = "2024-11-05";
        readonly IToolInvoker _tools;
        readonly ServerInfo _info;

        public McpRouter(IToolInvoker tools, ServerInfo info) { _tools = tools; _info = info; }

        public string Handle(string requestJson)
        {
            JObject req;
            try { req = JObject.Parse(requestJson); }
            catch { return JsonRpc.Error(null, -32700, "Parse error"); }

            string method = (string)req["method"];
            JToken id = req["id"];

            // нет id → нотификация: ответа не шлём
            if (id == null && method != null && method.StartsWith("notifications/"))
                return "";

            switch (method)
            {
                case "initialize":
                    return JsonRpc.Result(id, new JObject
                    {
                        ["protocolVersion"] = ProtocolVersion,
                        ["capabilities"] = new JObject { ["tools"] = new JObject() },
                        ["serverInfo"] = new JObject { ["name"] = _info.Name, ["version"] = _info.Version },
                        ["instructions"] = _info.Instructions ?? ""
                    });

                case "tools/list":
                    return JsonRpc.Result(id, new JObject { ["tools"] = _tools.ListTools() });

                case "tools/call":
                {
                    var p = (JObject)req["params"] ?? new JObject();
                    string name = (string)p["name"];
                    var args = (JObject)p["arguments"] ?? new JObject();
                    try
                    {
                        var result = _tools.Invoke(name, args);
                        return JsonRpc.Result(id, new JObject
                        {
                            ["content"] = new JArray { new JObject { ["type"] = "text", ["text"] = result.ToString() } },
                            ["isError"] = false
                        });
                    }
                    catch (System.Exception e)
                    {
                        return JsonRpc.Result(id, new JObject
                        {
                            ["content"] = new JArray { new JObject { ["type"] = "text", ["text"] = "Error: " + e.Message } },
                            ["isError"] = true
                        });
                    }
                }

                default:
                    return JsonRpc.Error(id, -32601, "Method not found: " + method);
            }
        }
    }
}
```

- [ ] **Step 5: PASS обоих наборов.** Run: `run_tests "ShtlMcp.Editor.Tests.JsonRpcTests"`, затем `run_tests "ShtlMcp.Editor.Tests.McpRouterTests"`

- [ ] **Step 6: Commit**

```bash
git add Packages/com.shtl.mcp/Editor/Transport Packages/com.shtl.mcp/Tests/Editor/JsonRpcTests.cs Packages/com.shtl.mcp/Tests/Editor/McpRouterTests.cs
git commit -m "feat(m1): minimal MCP JSON-RPC router (initialize/tools.list/tools.call)"
```

---

### Task 9: Инструменты — ToolRegistry + status + get_logs

**Files:**
- Create: `Packages/com.shtl.mcp/Editor/Tools/ITool.cs`
- Create: `Packages/com.shtl.mcp/Editor/Tools/IEditorContext.cs`
- Create: `Packages/com.shtl.mcp/Editor/Tools/ToolRegistry.cs`
- Create: `Packages/com.shtl.mcp/Editor/Tools/StatusTool.cs`
- Create: `Packages/com.shtl.mcp/Editor/Tools/GetLogsTool.cs`
- Test: `Packages/com.shtl.mcp/Tests/Editor/StatusToolTests.cs`
- Test: `Packages/com.shtl.mcp/Tests/Editor/GetLogsToolTests.cs`

- [ ] **Step 1: Падающие тесты**

`Tests/Editor/StatusToolTests.cs`:
```csharp
using NUnit.Framework;
using Newtonsoft.Json.Linq;
using ShtlMcp.Tools;

namespace ShtlMcp.Editor.Tests
{
    class FakeContext : IEditorContext
    {
        public string ProjectName => "PerfectWar";
        public string ProjectPath => "/p";
        public string UnityVersion => "2022.3.40f1";
        public string ServerName => "unity-perfectwar";
        public int Port => 9712;
        public int Pid => 4242;
        public bool IsPlaying => false;
        public bool IsCompiling => false;
        public double UptimeSeconds => 75;
        public int ClientCount => 1;
    }

    public class StatusToolTests
    {
        [Test] public void Invoke_ReturnsIdentityAndMode()
        {
            var o = new StatusTool(new FakeContext()).Invoke(new JObject());
            Assert.AreEqual("PerfectWar", (string)o["projectName"]);
            Assert.AreEqual("unity-perfectwar", (string)o["serverName"]);
            Assert.AreEqual(9712, (int)o["port"]);
            Assert.AreEqual("edit", (string)o["mode"]);
            Assert.AreEqual("ok", (string)o["health"]);
        }

        [Test] public void Schema_IsObjectType()
            => Assert.AreEqual("object", (string)new StatusTool(new FakeContext()).InputSchema["type"]);
    }
}
```

`Tests/Editor/GetLogsToolTests.cs`:
```csharp
using NUnit.Framework;
using Newtonsoft.Json.Linq;
using ShtlMcp.Logging;
using ShtlMcp.Tools;

namespace ShtlMcp.Editor.Tests
{
    public class GetLogsToolTests
    {
        [Test] public void Invoke_ReturnsRecentLogs_RespectingCount()
        {
            var buf = new LogBuffer(10);
            buf.Add("first", "", LogLevel.Info);
            buf.Add("boom", "stack", LogLevel.Error);
            var tool = new GetLogsTool(buf);

            var o = tool.Invoke(new JObject { ["count"] = 1 });
            var logs = (JArray)o["logs"];
            Assert.AreEqual(1, logs.Count);
            Assert.AreEqual("boom", (string)logs[0]["message"]);
            Assert.AreEqual("error", (string)logs[0]["level"]);
        }

        [Test] public void Invoke_FiltersByMinLevel()
        {
            var buf = new LogBuffer(10);
            buf.Add("i", "", LogLevel.Info);
            buf.Add("e", "", LogLevel.Error);
            var o = new GetLogsTool(buf).Invoke(new JObject { ["minLevel"] = "error", ["count"] = 10 });
            Assert.AreEqual(1, ((JArray)o["logs"]).Count);
            Assert.AreEqual("e", (string)((JArray)o["logs"])[0]["message"]);
        }
    }
}
```

- [ ] **Step 2: FAIL.** Run: `run_tests "ShtlMcp.Editor.Tests.StatusToolTests"` и `run_tests "ShtlMcp.Editor.Tests.GetLogsToolTests"`

- [ ] **Step 3: Реализация интерфейсов**

`Editor/Tools/ITool.cs`:
```csharp
using Newtonsoft.Json.Linq;

namespace ShtlMcp.Tools
{
    public interface ITool
    {
        string Name { get; }
        string Description { get; }
        JObject InputSchema { get; }
        bool NeedsMainThread { get; }
        JObject Invoke(JObject args);
    }
}
```

`Editor/Tools/IEditorContext.cs`:
```csharp
namespace ShtlMcp.Tools
{
    public interface IEditorContext
    {
        string ProjectName { get; }
        string ProjectPath { get; }
        string UnityVersion { get; }
        string ServerName { get; }
        int Port { get; }
        int Pid { get; }
        bool IsPlaying { get; }
        bool IsCompiling { get; }
        double UptimeSeconds { get; }
        int ClientCount { get; }
    }
}
```

- [ ] **Step 4: Реализация status/get_logs**

`Editor/Tools/StatusTool.cs`:
```csharp
using Newtonsoft.Json.Linq;

namespace ShtlMcp.Tools
{
    public sealed class StatusTool : ITool
    {
        readonly IEditorContext _ctx;
        public StatusTool(IEditorContext ctx) => _ctx = ctx;

        public string Name => "status";
        public string Description => "Identity and health of this Unity instance (project, port, mode, compiling).";
        public bool NeedsMainThread => true; // читает EditorApplication.*
        public JObject InputSchema => new JObject { ["type"] = "object", ["properties"] = new JObject() };

        public JObject Invoke(JObject args) => new JObject
        {
            ["projectName"] = _ctx.ProjectName,
            ["projectPath"] = _ctx.ProjectPath,
            ["unityVersion"] = _ctx.UnityVersion,
            ["serverName"] = _ctx.ServerName,
            ["port"] = _ctx.Port,
            ["pid"] = _ctx.Pid,
            ["mode"] = _ctx.IsPlaying ? "play" : "edit",
            ["isCompiling"] = _ctx.IsCompiling,
            ["uptimeSeconds"] = (int)_ctx.UptimeSeconds,
            ["clients"] = _ctx.ClientCount,
            ["health"] = "ok"
        };
    }
}
```

`Editor/Tools/GetLogsTool.cs`:
```csharp
using Newtonsoft.Json.Linq;
using ShtlMcp.Logging;

namespace ShtlMcp.Tools
{
    public sealed class GetLogsTool : ITool
    {
        readonly LogBuffer _buffer;
        public GetLogsTool(LogBuffer buffer) => _buffer = buffer;

        public string Name => "get_logs";
        public string Description => "Recent Unity console logs (filter by minLevel: info|warning|error).";
        public bool NeedsMainThread => false; // LogBuffer потокобезопасен
        public JObject InputSchema => new JObject
        {
            ["type"] = "object",
            ["properties"] = new JObject
            {
                ["minLevel"] = new JObject { ["type"] = "string", ["enum"] = new JArray { "info", "warning", "error" } },
                ["count"] = new JObject { ["type"] = "integer", ["default"] = 50 }
            }
        };

        public JObject Invoke(JObject args)
        {
            int count = (int?)args["count"] ?? 50;
            LogLevel? min = ParseLevel((string)args["minLevel"]);
            var arr = new JArray();
            foreach (var it in _buffer.Get(min, count))
                arr.Add(new JObject
                {
                    ["message"] = it.Message,
                    ["level"] = it.Level.ToString().ToLowerInvariant(),
                    ["stack"] = it.Stack
                });
            return new JObject { ["logs"] = arr };
        }

        static LogLevel? ParseLevel(string s)
        {
            switch (s)
            {
                case "warning": return LogLevel.Warning;
                case "error": return LogLevel.Error;
                case "info": return LogLevel.Info;
                default: return null;
            }
        }
    }
}
```

- [ ] **Step 5: Реализация ToolRegistry (IToolInvoker)**

`Editor/Tools/ToolRegistry.cs`:
```csharp
using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using ShtlMcp.Transport;

namespace ShtlMcp.Tools
{
    public sealed class ToolRegistry
    {
        readonly Dictionary<string, ITool> _tools = new Dictionary<string, ITool>();
        public void Register(ITool tool) => _tools[tool.Name] = tool;
        public ITool Get(string name) => _tools.TryGetValue(name, out var t) ? t : null;

        public JArray List()
        {
            var arr = new JArray();
            foreach (var t in _tools.Values)
                arr.Add(new JObject
                {
                    ["name"] = t.Name,
                    ["description"] = t.Description,
                    ["inputSchema"] = t.InputSchema
                });
            return arr;
        }
    }
}
```

- [ ] **Step 6: PASS обоих наборов.** Run: `run_tests "ShtlMcp.Editor.Tests.StatusToolTests"`, `run_tests "ShtlMcp.Editor.Tests.GetLogsToolTests"`

- [ ] **Step 7: Commit**

```bash
git add Packages/com.shtl.mcp/Editor/Tools Packages/com.shtl.mcp/Tests/Editor/StatusToolTests.cs Packages/com.shtl.mcp/Tests/Editor/GetLogsToolTests.cs
git commit -m "feat(m1): tool registry + status + get_logs tools"
```

---

### Task 10: HttpServer + DispatchingToolInvoker (integration)

**Files:**
- Create: `Packages/com.shtl.mcp/Editor/Server/HttpServer.cs`
- Create: `Packages/com.shtl.mcp/Editor/Server/DispatchingToolInvoker.cs`

> Эта задача — сетевая склейка; автоматический unit-тест на `HttpListener` хрупок. Верификация — ручная (Step 4) и через end-to-end Task 13. Логика, которую он связывает, уже покрыта тестами Task 6/8/9.

- [ ] **Step 1: Реализация DispatchingToolInvoker**

`Editor/Server/DispatchingToolInvoker.cs`:
```csharp
using Newtonsoft.Json.Linq;
using ShtlMcp.Dispatch;
using ShtlMcp.Tools;
using ShtlMcp.Transport;

namespace ShtlMcp.Server
{
    /// Маршалит вызовы инструментов в главный поток, когда NeedsMainThread.
    public sealed class DispatchingToolInvoker : IToolInvoker
    {
        readonly ToolRegistry _registry;
        readonly MainThreadDispatcher _dispatcher;
        const int TimeoutMs = 5000;

        public DispatchingToolInvoker(ToolRegistry registry, MainThreadDispatcher dispatcher)
        { _registry = registry; _dispatcher = dispatcher; }

        public JArray ListTools() => _registry.List();

        public JObject Invoke(string name, JObject args)
        {
            var tool = _registry.Get(name);
            if (tool == null) throw new System.Exception("Unknown tool: " + name);
            return tool.NeedsMainThread
                ? _dispatcher.RunOnMain(() => tool.Invoke(args), TimeoutMs)
                : tool.Invoke(args);
        }
    }
}
```

- [ ] **Step 2: Реализация HttpServer**

`Editor/Server/HttpServer.cs`:
```csharp
using System;
using System.IO;
using System.Net;
using System.Text;
using System.Threading;

namespace ShtlMcp.Server
{
    /// Фоновый HttpListener на 127.0.0.1:port. Любой путь трактуется как MCP-эндпойнт.
    public sealed class HttpServer
    {
        readonly Func<string, string> _handle;     // McpRouter.Handle
        readonly Action _onRequest;                 // отметка активности (clients/uptime)
        HttpListener _listener;
        Thread _thread;
        volatile bool _running;

        public int Port { get; }
        public bool IsListening => _listener != null && _listener.IsListening;

        public HttpServer(int port, Func<string, string> handle, Action onRequest)
        { Port = port; _handle = handle; _onRequest = onRequest; }

        public void Start()
        {
            if (_running) return;
            _listener = new HttpListener();
            _listener.Prefixes.Add($"http://127.0.0.1:{Port}/");
            _listener.Start();
            _running = true;
            _thread = new Thread(Loop) { IsBackground = true, Name = "ShtlMcpHttp" };
            _thread.Start();
        }

        void Loop()
        {
            while (_running)
            {
                HttpListenerContext ctx;
                try { ctx = _listener.GetContext(); }
                catch { break; } // listener остановлен
                try
                {
                    _onRequest?.Invoke();
                    if (ctx.Request.HttpMethod != "POST")
                    {
                        ctx.Response.StatusCode = 405; // нет server-initiated SSE в M1
                    }
                    else
                    {
                        string body;
                        using (var r = new StreamReader(ctx.Request.InputStream,
                                   ctx.Request.ContentEncoding ?? Encoding.UTF8))
                            body = r.ReadToEnd();

                        string resp = _handle(body) ?? "";
                        var bytes = Encoding.UTF8.GetBytes(resp);
                        ctx.Response.ContentType = "application/json";
                        ctx.Response.StatusCode = resp.Length == 0 ? 202 : 200;
                        ctx.Response.OutputStream.Write(bytes, 0, bytes.Length);
                    }
                }
                catch { /* одиночный сбойный запрос не валит цикл */ }
                finally { try { ctx.Response.OutputStream.Close(); } catch { } }
            }
        }

        public void Stop()
        {
            _running = false;
            try { _listener?.Stop(); _listener?.Close(); } catch { }
            _listener = null;
        }
    }
}
```

- [ ] **Step 3: Скомпилировать**

```bash
"$UNITY" -batchmode -quit -projectPath "$(pwd)" -logFile - | grep -iE "error CS" && echo "ERRORS" || echo "OK"
```
Expected: `OK`.

- [ ] **Step 4: Ручная проверка `curl` (после Task 11 сервер стартует сам; если выполняете Task 10 изолированно — временно вызовите Start() из [MenuItem], затем удалите).** Полная проверка — в Task 13.

- [ ] **Step 5: Commit**

```bash
git add Packages/com.shtl.mcp/Editor/Server
git commit -m "feat(m1): HttpListener server + dispatching tool invoker"
```

---

### Task 11: Lifecycle — Bootstrap, выживание при reload, watchdog, heartbeat

**Files:**
- Create: `Packages/com.shtl.mcp/Editor/Lifecycle/ShtlMcpConfig.cs`
- Create: `Packages/com.shtl.mcp/Editor/Lifecycle/EditorContext.cs`
- Create: `Packages/com.shtl.mcp/Editor/Lifecycle/ShtlMcpServer.cs`
- Create: `Packages/com.shtl.mcp/Editor/Lifecycle/ShtlMcpBootstrap.cs`

> Integration. Верификация выживания при reload — полуавтоматическая (Step 5) + ручная (Task 13). Чистая логика внутри уже покрыта.

- [ ] **Step 1: Config + EditorContext**

`Editor/Lifecycle/ShtlMcpConfig.cs`:
```csharp
using UnityEditor;

namespace ShtlMcp.Lifecycle
{
    public static class ShtlMcpConfig
    {
        const string EnabledKey = "ShtlMcp.Enabled";
        public static bool Enabled
        {
            get => EditorPrefs.GetBool(EnabledKey, true);
            set => EditorPrefs.SetBool(EnabledKey, value);
        }
    }
}
```

`Editor/Lifecycle/EditorContext.cs`:
```csharp
using System;
using System.Diagnostics;
using UnityEditor;
using UnityEngine;
using ShtlMcp.Tools;

namespace ShtlMcp.Lifecycle
{
    public sealed class EditorContext : IEditorContext
    {
        readonly Func<int> _port;
        readonly Func<string> _serverName;
        readonly Func<double> _uptime;
        readonly Func<int> _clients;

        public EditorContext(Func<int> port, Func<string> serverName, Func<double> uptime, Func<int> clients)
        { _port = port; _serverName = serverName; _uptime = uptime; _clients = clients; }

        public string ProjectName => Application.productName;
        public string ProjectPath => System.IO.Directory.GetParent(Application.dataPath).FullName;
        public string UnityVersion => Application.unityVersion;
        public string ServerName => _serverName();
        public int Port => _port();
        public int Pid => Process.GetCurrentProcess().Id;
        public bool IsPlaying => EditorApplication.isPlaying;
        public bool IsCompiling => EditorApplication.isCompiling;
        public double UptimeSeconds => _uptime();
        public int ClientCount => _clients();
    }
}
```

- [ ] **Step 2: ShtlMcpServer (фасад)**

`Editor/Lifecycle/ShtlMcpServer.cs`:
```csharp
using System;
using System.Net;
using System.Net.Sockets;
using UnityEditor;
using UnityEngine;
using ShtlMcp.Common;
using ShtlMcp.Dispatch;
using ShtlMcp.Logging;
using ShtlMcp.Registry;
using ShtlMcp.Server;
using ShtlMcp.Tools;
using ShtlMcp.Transport;

namespace ShtlMcp.Lifecycle
{
    public sealed class ShtlMcpServer
    {
        static ShtlMcpServer _instance;
        public static ShtlMcpServer Instance => _instance ?? (_instance = new ShtlMcpServer());

        const string PortKey = "ShtlMcp.Port";
        const string StartedKey = "ShtlMcp.StartedTicks";
        static readonly TimeSpan Ttl = TimeSpan.FromSeconds(30);
        static readonly string RegistryPath = System.IO.Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".unity-mcp", "registry.json");

        readonly MainThreadDispatcher _dispatcher = new MainThreadDispatcher();
        readonly LogBuffer _logs = new LogBuffer(500);
        readonly ToolRegistry _tools = new ToolRegistry();
        readonly RegistryStore _registry = new RegistryStore(RegistryPath);

        HttpServer _http;
        string _serverName;
        DateTime _lastRequestUtc = DateTime.MinValue;

        public int Port { get; private set; }
        public string ServerName => _serverName;
        public bool IsListening => _http != null && _http.IsListening;

        string ProjectPath => System.IO.Directory.GetParent(Application.dataPath).FullName;
        double Uptime => (DateTime.UtcNow - new DateTime(long.Parse(
            SessionState.GetString(StartedKey, DateTime.UtcNow.Ticks.ToString())), DateTimeKind.Utc)).TotalSeconds;

        public void EnsureStarted()
        {
            if (IsListening) return;
            if (SessionState.GetString(StartedKey, "") == "")
                SessionState.SetString(StartedKey, DateTime.UtcNow.Ticks.ToString());

            Port = ResolvePort();
            _serverName = ShtlMcp.Common.ServerName.Resolve(Application.productName, ProjectPath,
                name => _registry.LivePathForName(name, Ttl));

            // подписка на логи (потокобезопасный вариант)
            Application.logMessageReceivedThreaded -= OnLog;
            Application.logMessageReceivedThreaded += OnLog;

            // dispatcher качается главным потоком
            EditorApplication.update -= _dispatcher.Drain;
            EditorApplication.update += _dispatcher.Drain;

            // инструменты
            var ctx = new EditorContext(() => Port, () => _serverName, () => Uptime, ClientCount);
            _tools.Register(new StatusTool(ctx));
            _tools.Register(new GetLogsTool(_logs));

            var invoker = new DispatchingToolInvoker(_tools, _dispatcher);
            var info = new ServerInfo
            {
                Name = _serverName,
                Version = "0.1.0",
                Instructions = "Unity MCP. Если станет недоступен — читай ~/.unity-mcp/registry.json."
            };
            var router = new McpRouter(invoker, info);

            _http = new HttpServer(Port, router.Handle, () => _lastRequestUtc = DateTime.UtcNow);
            _http.Start();
            Heartbeat();
        }

        public void StopListenerForReload()
        {
            _http?.Stop();
            _http = null; // порт остаётся в SessionState → переподнимем тот же
        }

        public void RestartNow()
        {
            StopListenerForReload();
            EnsureStarted();
        }

        public void WatchdogTick()
        {
            if (!ShtlMcpConfig.Enabled) { StopListenerForReload(); return; }
            if (!IsListening) EnsureStarted();
            Heartbeat();
        }

        int ClientCount() => (DateTime.UtcNow - _lastRequestUtc) < TimeSpan.FromSeconds(30) ? 1 : 0;

        int ResolvePort()
        {
            int saved = SessionState.GetInt(PortKey, 0);
            if (saved != 0) return saved;
            int port = PortAllocator.Allocate(ProjectPath, IsPortFree);
            SessionState.SetInt(PortKey, port);
            return port;
        }

        static bool IsPortFree(int port)
        {
            try
            {
                var l = new TcpListener(IPAddress.Loopback, port);
                l.Start(); l.Stop(); return true;
            }
            catch (SocketException) { return false; }
        }

        void OnLog(string message, string stack, LogType type)
            => _logs.Add(message, stack,
                type == LogType.Error || type == LogType.Exception || type == LogType.Assert ? LogLevel.Error :
                type == LogType.Warning ? LogLevel.Warning : LogLevel.Info);

        void Heartbeat()
        {
            try
            {
                _registry.Upsert(new InstanceEntry
                {
                    ProjectName = Application.productName,
                    ProjectPath = ProjectPath,
                    UnityVersion = Application.unityVersion,
                    ServerName = _serverName,
                    Port = Port,
                    Pid = System.Diagnostics.Process.GetCurrentProcess().Id,
                    Mode = EditorApplication.isPlaying ? "play" : "edit",
                    Compiling = EditorApplication.isCompiling,
                    StartedAt = new DateTime(long.Parse(SessionState.GetString(StartedKey,
                        DateTime.UtcNow.Ticks.ToString())), DateTimeKind.Utc),
                    LastHeartbeat = DateTime.UtcNow
                });
            }
            catch { /* реестр недоступен — не критично */ }
        }
    }
}
```

- [ ] **Step 3: Bootstrap ([InitializeOnLoad])**

`Editor/Lifecycle/ShtlMcpBootstrap.cs`:
```csharp
using UnityEditor;

namespace ShtlMcp.Lifecycle
{
    [InitializeOnLoad]
    public static class ShtlMcpBootstrap
    {
        static double _lastTick;

        static ShtlMcpBootstrap() => EditorApplication.delayCall += Init;

        static void Init()
        {
            if (!ShtlMcpConfig.Enabled) return;
            ShtlMcpServer.Instance.EnsureStarted();

            AssemblyReloadEvents.beforeAssemblyReload -= OnBeforeReload;
            AssemblyReloadEvents.beforeAssemblyReload += OnBeforeReload;

            EditorApplication.update -= Tick;
            EditorApplication.update += Tick;
        }

        static void OnBeforeReload() => ShtlMcpServer.Instance.StopListenerForReload();

        static void Tick()
        {
            double now = EditorApplication.timeSinceStartup;
            if (now - _lastTick < 1.0) return; // раз в секунду
            _lastTick = now;
            ShtlMcpServer.Instance.WatchdogTick();
        }
    }
}
```

- [ ] **Step 4: Скомпилировать**

```bash
"$UNITY" -batchmode -quit -projectPath "$(pwd)" -logFile - | grep -iE "error CS" && echo "ERRORS" || echo "OK"
```
Expected: `OK`.

- [ ] **Step 5: Полуавтоматическая проверка выживания (интерактивно)**

1. Открыть проект в Unity.
2. В консоли убедиться, что лог-старт без исключений; реестр создан: `cat ~/.unity-mcp/registry.json` → есть запись с твоим `projectPath` и портом.
3. `curl -s -X POST http://127.0.0.1:<port>/mcp -d '{"jsonrpc":"2.0","id":1,"method":"initialize","params":{}}'` → JSON с `serverInfo.name = unity-<project>`.
4. Изменить любой `.cs` (тронуть пробел) → дождаться recompile (domain reload).
5. Повторить `curl initialize` → снова 200 и тот же порт. **Выживание подтверждено.**
6. Войти в Play, повторить `curl` со `method:"tools/call", params:{name:"status"}` → `mode:"play"`.

- [ ] **Step 6: Commit**

```bash
git add Packages/com.shtl.mcp/Editor/Lifecycle
git commit -m "feat(m1): lifecycle bootstrap — reload survival, watchdog, registry heartbeat"
```

---

### Task 12: Минимальный дашборд (UI Toolkit)

**Files:**
- Create: `Packages/com.shtl.mcp/Editor/UI/DashboardWindow.cs`

> UI — ручная верификация (Step 3).

- [ ] **Step 1: Реализация окна**

`Editor/UI/DashboardWindow.cs`:
```csharp
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using ShtlMcp.Lifecycle;

namespace ShtlMcp.UI
{
    public sealed class DashboardWindow : EditorWindow
    {
        [MenuItem("Window/Shtl MCP")]
        public static void Open() => GetWindow<DashboardWindow>("Shtl MCP");

        Label _status, _identity, _mode;
        TextField _cmd;
        double _next;

        public void CreateGUI()
        {
            var root = rootVisualElement;
            root.style.paddingLeft = root.style.paddingTop = 8;
            _status = new Label(); _identity = new Label(); _mode = new Label();
            root.Add(_status); root.Add(_identity); root.Add(_mode);

            root.Add(new Label("claude mcp add command:") { style = { marginTop = 8 } });
            _cmd = new TextField { isReadOnly = true, multiline = true };
            root.Add(_cmd);

            var copy = new Button(() => EditorGUIUtility.systemCopyBuffer = _cmd.value) { text = "Copy" };
            var restart = new Button(() => ShtlMcpServer.Instance.RestartNow()) { text = "Restart server" };
            var row = new VisualElement { style = { flexDirection = FlexDirection.Row, marginTop = 8 } };
            row.Add(copy); row.Add(restart);
            root.Add(row);

            Refresh();
        }

        void Update()
        {
            if (EditorApplication.timeSinceStartup < _next) return;
            _next = EditorApplication.timeSinceStartup + 1.0;
            Refresh();
        }

        void Refresh()
        {
            if (_status == null) return;
            var s = ShtlMcpServer.Instance;
            bool up = s.IsListening;
            _status.text = (up ? "● running" : "○ stopped") + "   " + s.ServerName + "   :" + s.Port;
            _identity.text = "project: " + Application.productName;
            _mode.text = "mode: " + (EditorApplication.isPlaying ? "PLAY" : "EDIT");
            _cmd.value = $"claude mcp add --transport http {s.ServerName} http://127.0.0.1:{s.Port}/mcp";
        }
    }
}
```

- [ ] **Step 2: Скомпилировать**

```bash
"$UNITY" -batchmode -quit -projectPath "$(pwd)" -logFile - | grep -iE "error CS" && echo "ERRORS" || echo "OK"
```
Expected: `OK`.

- [ ] **Step 3: Ручная проверка** — открыть **Window → Shtl MCP**: видно `● running`, имя, порт, режим, готовую команду; **Copy** кладёт в буфер; **Restart server** переподнимает (статус мигает stopped→running).

- [ ] **Step 4: Commit**

```bash
git add Packages/com.shtl.mcp/Editor/UI
git commit -m "feat(m1): minimal UI Toolkit dashboard"
```

---

### Task 13: End-to-end через Claude Code + закрытие документации

**Files:**
- Modify: `wiki/index.md` (добавить `code/` reference-страницы)
- Create: `wiki/code/m1-server.md` (reference: модули M1, pin к коммиту)
- Modify: `wiki/log.md`, `wiki/tasks/m1-walking-skeleton/journal.md`, `TASK.md` (status: done)

- [ ] **Step 1: E2E из Claude Code**

```bash
PORT=$(python3 -c "import json,os;print([e for e in json.load(open(os.path.expanduser('~/.unity-mcp/registry.json'))) if e['projectName']][0]['port'])")
claude mcp add --transport http unity-$(...) http://127.0.0.1:$PORT/mcp   # точную строку взять из дашборда (Copy)
```
В новой сессии Claude Code: вызвать `mcp__unity-<project>__status` → идентичность; `mcp__unity-<project>__get_logs` → последние логи.
Expected: оба инструмента отвечают; `status.mode` отражает edit/play.

- [ ] **Step 2: Тест выживания в реальном сценарии**

Из Claude Code/редактора инициировать recompile (изменить скрипт), затем снова вызвать `status`. После короткого окна — успех (reconnect при необходимости: `claude mcp` reconnect).
Expected: `status` снова отвечает; порт прежний.

- [ ] **Step 3: Прогнать ВСЕ EditMode-тесты**

Run: `"$UNITY" -batchmode -runTests -projectPath "$(pwd)" -testPlatform EditMode -testResults "$(pwd)/TestResults.xml" -logFile -`
Expected: все наборы PASS (`exit=0`).

- [ ] **Step 4: Reference-страница кода + журнал**

Создать `wiki/code/m1-server.md` (frontmatter `content_class: housekeeping`/`code`, `compiled_at_commit: <hash HEAD>`): карта модулей M1 (Transport/Dispatch/Registry/Tools/Server/Lifecycle/UI) и их публичных контрактов. Добавить ссылку в `wiki/index.md` (секция `code/`). Дописать `wiki/log.md`: `## [дата] forward | M1 walking skeleton | wiki/tasks/m1-walking-skeleton`. Заполнить `journal.md` (что сделано, отклонение по asmdef, результаты верификации). В `TASK.md` — `status: done`.

- [ ] **Step 5: Commit**

```bash
git add wiki/
git commit -m "docs(m1): code reference + journal; M1 walking skeleton done"
```

---

## Self-Review (выполнено при написании плана)

**1. Spec coverage (срез M1):** F1 → Task 3/4/5/11 (порт, serverName, реестр, heartbeat, `claude mcp add`). F2 → Task 11 (watchdog/respawn) + Task 12 (Restart). F3-частично → Task 9 (`status`, `get_logs` со схемой). F4 → Task 6 (главный поток) + Task 11 (reload survival, AssemblyReloadEvents, SessionState). F5 → Task 12. F6 → Task 1 (только пакет, Newtonsoft) — без внешних процессов. Async-job/control-flag/F7/run_csharp — осознанно вне M1 (→ M2/M3, отмечено в TASK.md).

**2. Placeholder scan:** код приведён полностью в каждом шаге; «expected output» конкретны. Шаги Unity-склейки (Task 10–12) помечены как integration/manual с явной процедурой проверки — это не плейсхолдеры, а адекватный способ верификации сетевого/UI кода.

**3. Type consistency:** `IEditorContext` (Task 9) реализуется `EditorContext` (Task 11) — сигнатуры сверены. `IToolInvoker` (Task 8: `ListTools`/`Invoke`) реализуется `DispatchingToolInvoker` (Task 10) и фейком в тестах (Task 8) — совпадает. `ITool` (`Name/Description/InputSchema/NeedsMainThread/Invoke`) — у `StatusTool`/`GetLogsTool` (Task 9) и в `ToolRegistry.List()`. `LogBuffer.Get(LogLevel?, int)` (Task 7) вызывается в `GetLogsTool` (Task 9) и тестах — совпадает. `ShtlMcpServer` методы (`EnsureStarted/StopListenerForReload/RestartNow/WatchdogTick/IsListening/Port/ServerName`) используются в `Bootstrap` (Task 11) и `DashboardWindow` (Task 12) — совпадает. `RegistryStore` (`Upsert/Read/LivePathForName/Prune`) — Task 5 ↔ Task 11.
