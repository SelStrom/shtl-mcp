using System.Collections;
using System.Threading.Tasks;
using NUnit.Framework;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using Shtl.Mcp.Lifecycle;

namespace Shtl.Mcp.Editor.Tests
{
    /// M5/AC3.9 e2e: компиляционный путь write_asset — запись .cs через реальный HTTP tools/call →
    /// jobId → domain reload → get_job отдаёт результат ПОСЛЕ reload (reload-job-канал, INV-1).
    /// Паттерн reload-survival — как ReloadSurvivalTests ([SerializeField]-поля переживают reload,
    /// вложенные энумераторы пампятся вручную).
    public class WriteAssetReloadTests
    {
        const string ProbePath = "Assets/ShtlM5ReloadProbe.cs";
        const string ProbeSource = "// m5 e2e: write_asset compile path\npublic static class ShtlM5ReloadProbe {}\n";

        // Переживают domain reload (JsonUtility-сериализация fixture). НЕ static, НЕ property.
        [SerializeField] int _port;
        [SerializeField] string _jobId;

        [UnityTest]
        [Category("DomainReload")]
        public IEnumerator WriteAsset_Cs_JobCompletesAfterReload()
        {
            ShtlMcpServer.Instance.EnsureStarted();
            Assert.That(ShtlMcpServer.Instance.IsListening, Is.True, "сервер должен слушать до теста");
            _port = ShtlMcpServer.Instance.Port;

            // write_asset через провод: job создаётся в JobStore реального сервера
            Task<string> write = McpProbe.CallToolAsync(_port, "write_asset", new JObject
            {
                ["path"] = ProbePath,
                ["content"] = ProbeSource
            });
            while (!write.IsCompleted)
            {
                yield return null;
            }
            Assert.That(write.Status, Is.EqualTo(TaskStatus.RanToCompletion), "write_asset должен ответить до reload");
            Assert.That(McpProbe.TryGetToolPayload(write.Result, out var begin), Is.True, write.Result);
            Assert.That((string)begin["error"], Is.Null, "write_asset не должен упасть: " + begin);
            _jobId = (string)begin["jobId"];
            Assert.That(_jobId, Is.Not.Null.And.Not.Empty, "компилируемый .cs + refresh → jobId: " + begin);

            // импорт нового .cs триггерит компиляцию + reload; тест возобновится после него
            yield return new WaitForDomainReload();

            // финализация job — после reload (сервер: WatchdogTick → ReloadJobs.FinalizeOnTick)
            var done = new bool[1];
            var poll = PollJobDone(_port, _jobId, 30, done);
            while (poll.MoveNext())
            {
                yield return poll.Current;
            }
            Assert.That(done[0], Is.True, "get_job должен отдать терминальный статус после reload");

            Task<string> final = McpProbe.CallToolAsync(_port, "get_job", new JObject { ["jobId"] = _jobId });
            while (!final.IsCompleted)
            {
                yield return null;
            }
            Assert.That(McpProbe.TryGetToolPayload(final.Result, out var job), Is.True, final.Result);
            Assert.That((string)job["status"], Is.EqualTo("done"), "валидный .cs → done: " + job);
            StringAssert.Contains("recompiled", (string)job["result"], "результат — recompiled");

            // cleanup: удалить пробный скрипт; удаление тоже компилирует → дождаться reload в тесте,
            // чтобы он не ударил по следующим тестам посреди прогона
            AssetDatabase.DeleteAsset(ProbePath);
            EditorUtility.RequestScriptReload();
            yield return new WaitForDomainReload();
        }

        /// yield-poll get_job, пока статус не терминальный (или таймаут). WatchdogTick дёргаем явно —
        /// финализация не должна зависеть от расписания фонового watchdog в тестовом прогоне.
        static IEnumerator PollJobDone(int port, string jobId, double timeoutSec, bool[] result)
        {
            result[0] = false;
            double deadline = EditorApplication.timeSinceStartup + timeoutSec;
            while (EditorApplication.timeSinceStartup < deadline)
            {
                ShtlMcpServer.Instance.WatchdogTick();
                Task<string> call = McpProbe.CallToolAsync(port, "get_job", new JObject { ["jobId"] = jobId });
                while (!call.IsCompleted)
                {
                    yield return null;
                }
                if (call.Status == TaskStatus.RanToCompletion
                    && McpProbe.TryGetToolPayload(call.Result, out var job)
                    && (string)job["status"] != "running")
                {
                    result[0] = true;
                    yield break;
                }
                if (call.Status != TaskStatus.RanToCompletion)
                {
                    _ = call.Exception; // observe faulted task (листенер ещё поднимается после reload)
                }
                yield return null;
            }
        }
    }
}
