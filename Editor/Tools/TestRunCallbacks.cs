using UnityEditor;
using UnityEngine;
using UnityEditor.TestTools.TestRunner.Api;
using Newtonsoft.Json.Linq;
using Shtl.Mcp.Jobs;

namespace Shtl.Mcp.Tools
{
    /// Колбэки Unity Test Runner: по завершении прогона пишут результат в job (через SessionState-маркер
    /// in-flight jobId) — независимо от того, пережил ли исходный caller domain reload.
    public sealed class TestRunCallbacks : ICallbacks
    {
        static TestRunnerApi _reattachApi; // держим живым после reattach

        readonly JobStore _jobs;

        // JobStore инъектируется (не ShtlMcpServer.Instance): иначе Tools→Lifecycle замыкал бы цикл сборок.
        public TestRunCallbacks(JobStore jobs)
        {
            _jobs = jobs;
        }

        /// После domain reload: если есть in-flight прогон (маркер в SessionState) — переподписаться,
        /// чтобы RunFinished долетел в новый домен и завершил job. Вызывается из EnsureStarted (главный поток).
        public static void ReattachIfPending(JobStore jobs)
        {
            var jobId = SessionState.GetString(RunTestsTool.JobMarkerKey, "");
            if (string.IsNullOrEmpty(jobId))
            {
                return;
            }
            if (_reattachApi != null)
            {
                Object.DestroyImmediate(_reattachApi); // не плодить ScriptableObject при повторных reload
            }
            _reattachApi = ScriptableObject.CreateInstance<TestRunnerApi>();
            _reattachApi.hideFlags = HideFlags.HideAndDontSave;
            _reattachApi.RegisterCallbacks(new TestRunCallbacks(jobs));
        }

        int _total, _completed;
        string _current;

        public void RunStarted(ITestAdaptor testsToRun)
        {
            TestRunnerNoThrottle.Apply(); // повторно (идемпотентно): прогон стартовал — держим full-rate
            _total = testsToRun.TestCaseCount;
            _completed = 0;
            PushProgress();
        }

        public void TestStarted(ITestAdaptor test)
        {
            if (!test.HasChildren) // только листья (реальные тесты), не сьюты
            {
                _current = test.Name;
                PushProgress();
            }
        }

        // Прогресс — best-effort в job (get_job отдаёт его для running). После reattach счётчик с нуля.
        void PushProgress()
        {
            var jobId = SessionState.GetString(RunTestsTool.JobMarkerKey, "");
            if (!string.IsNullOrEmpty(jobId))
            {
                _jobs.SetProgress(jobId, _completed, _total, _current);
            }
        }

        public void TestFinished(ITestResultAdaptor result)
        {
            if (!result.HasChildren)
            {
                _completed++;
                PushProgress();
            }
        }

        public void RunFinished(ITestResultAdaptor result)
        {
            var jobId = SessionState.GetString(RunTestsTool.JobMarkerKey, "");
            if (string.IsNullOrEmpty(jobId))
            {
                return;
            }

            var failures = new JArray();
            Collect(result, failures);
            var payload = new JObject
            {
                ["passed"] = result.PassCount,
                ["failed"] = result.FailCount,
                ["skipped"] = result.SkipCount,
                ["status"] = result.TestStatus.ToString(),
                ["failures"] = failures
            };
            _jobs.Complete(jobId, payload.ToString());
            SessionState.EraseString(RunTestsTool.JobMarkerKey);
            TestRunnerNoThrottle.Restore(); // вернуть троттлинг (внутри marker-guard → дубли колбэков не двойнят)
            PlayModeOptionsGuard.Restore();  // вернуть enterPlayModeOptions (идемпотентно: EditMode-прогон не трогал)
        }

        // Обходим дерево результатов, собираем упавшие листья (имя + сообщение).
        static void Collect(ITestResultAdaptor result, JArray acc)
        {
            if (result.HasChildren)
            {
                foreach (var child in result.Children)
                {
                    Collect(child, acc);
                }
                return;
            }
            if (result.TestStatus == TestStatus.Failed)
            {
                acc.Add(new JObject
                {
                    ["name"] = result.FullName,
                    ["message"] = result.Message
                });
            }
        }
    }
}
