using NUnit.Framework;
using UnityEditor;
using Shtl.Mcp.Jobs;

namespace Shtl.Mcp.Editor.Tests
{
    public class JobStoreTests
    {
        // Изолированный ключ: НЕ трогаем live-ключ работающего сервера (один SessionState на редактор;
        // дефолтный ключ затёр бы live-job, см. JobStore). Чистим до/после для изоляции между тестами.
        const string Key = "Shtl.Mcp.Jobs.Test.JobStoreTests";

        [SetUp]
        public void SetUp()
        {
            SessionState.EraseString(Key);
        }

        [TearDown]
        public void TearDown()
        {
            SessionState.EraseString(Key);
        }

        [Test]
        public void Create_IsRunning()
        {
            var store = new JobStore(Key);
            var id = store.Create("recompile");
            var job = store.Get(id);
            Assert.IsNotNull(job);
            Assert.AreEqual("running", job.Status);
            Assert.AreEqual("recompile", job.Tool);
        }

        [Test]
        public void Complete_SetsDoneAndResult()
        {
            var store = new JobStore(Key);
            var id = store.Create("run_tests");
            store.Complete(id, "{\"passed\":3}");
            var job = store.Get(id);
            Assert.AreEqual("done", job.Status);
            StringAssert.Contains("passed", job.Result);
            Assert.IsNull(job.Error);
        }

        [Test]
        public void Fail_SetsFailedAndError()
        {
            var store = new JobStore(Key);
            var id = store.Create("set_play_mode");
            store.Fail(id, "boom");
            var job = store.Get(id);
            Assert.AreEqual("failed", job.Status);
            Assert.AreEqual("boom", job.Error);
        }

        [Test]
        public void Get_Unknown_ReturnsNull()
        {
            var store = new JobStore(Key);
            Assert.IsNull(store.Get("nope"));
        }

        // Идемпотентность финализации: повторный Complete/Fail не перезатирает терминальный статус
        // (защита от двойной финализации reload-job через два пути — OnPlayModeChanged + FinalizeOnTick).
        [Test]
        public void Finalize_OnTerminalJob_IsNoOp()
        {
            var store = new JobStore(Key);
            var id = store.Create("set_play_mode");
            store.Complete(id, "{\"v\":1}");
            store.Complete(id, "{\"v\":2}"); // повторно — игнор
            StringAssert.Contains("\"v\":1", store.Get(id).Result);

            store.Fail(id, "boom"); // fail после done — игнор
            Assert.AreEqual("done", store.Get(id).Status);
        }

        // AC-1: состояние job переживает domain reload — новый экземпляр JobStore читает из SessionState.
        [Test]
        public void Survives_Reload_ViaSessionState()
        {
            var before = new JobStore(Key);
            var id = before.Create("recompile");
            before.Complete(id, "{\"ok\":true}");

            // эмуляция reload: managed-стейт `before` исчез, новый экземпляр поднимается из SessionState
            var after = new JobStore(Key);
            var job = after.Get(id);

            Assert.IsNotNull(job, "job должен пережить reload через SessionState");
            Assert.AreEqual("done", job.Status);
            Assert.AreEqual("recompile", job.Tool);
            StringAssert.Contains("ok", job.Result);
        }
    }
}
