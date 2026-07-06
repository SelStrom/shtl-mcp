using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using NUnit.Framework;
using Shtl.Mcp.Registry;

namespace Shtl.Mcp.Editor.Tests
{
    /// Конкурентные Upsert нескольких «инстансов» не должны терять записи (lost update) — файловый lock.
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
                var lockFile = file + ".lock";
                if (File.Exists(lockFile)) { File.Delete(lockFile); }
            }
        }
    }
}
