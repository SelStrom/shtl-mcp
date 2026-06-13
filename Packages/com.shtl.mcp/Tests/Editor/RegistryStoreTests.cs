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
            store.Upsert(Entry("/p", "a", DateTime.UtcNow));
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
