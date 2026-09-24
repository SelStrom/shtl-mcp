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

            _jobs.Complete(jobId, BuildPayload(result).ToString());
            SessionState.EraseString(RunTestsTool.JobMarkerKey);
            TestRunnerNoThrottle.Restore(); // вернуть троттлинг (внутри marker-guard → дубли колбэков не двойнят)
            PlayModeOptionsGuard.Restore();  // вернуть enterPlayModeOptions (идемпотентно: EditMode-прогон не трогал)
        }

        // status — худший исход по счётчикам, а не TestStatus корня: NUnit сворачивает Inconclusive-детей
        // сьюта в Passed (сьют из одних Inconclusive тоже Passed), и Inconclusive выглядел бы как успех.
        internal static JObject BuildPayload(ITestResultAdaptor root)
        {
            var failures = new JArray();
            var inconclusive = new JArray();
            Collect(root, failures, inconclusive);
            return new JObject
            {
                ["passed"] = root.PassCount,
                ["failed"] = root.FailCount,
                ["skipped"] = root.SkipCount,
                ["inconclusive"] = root.InconclusiveCount,
                ["status"] = WorstStatus(root).ToString(),
                ["failures"] = failures,
                ["inconclusiveTests"] = inconclusive
            };
        }

        static TestStatus WorstStatus(ITestResultAdaptor root)
        {
            if (root.FailCount > 0 || root.TestStatus == TestStatus.Failed) // сбой уровня сьюта (OneTimeTearDown) не в FailCount
            {
                return TestStatus.Failed;
            }
            if (root.InconclusiveCount > 0)
            {
                return TestStatus.Inconclusive;
            }
            if (root.SkipCount > 0)
            {
                return TestStatus.Skipped;
            }
            return TestStatus.Passed;
        }

        // Обходим дерево результатов, собираем упавшие и inconclusive-листья (имя + сообщение).
        static void Collect(ITestResultAdaptor result, JArray failures, JArray inconclusive)
        {
            if (result.HasChildren)
            {
                foreach (var child in result.Children)
                {
                    Collect(child, failures, inconclusive);
                }
                return;
            }
            var acc = result.TestStatus == TestStatus.Failed ? failures
                : result.TestStatus == TestStatus.Inconclusive ? inconclusive
                : null;
            acc?.Add(new JObject
            {
                ["name"] = result.FullName,
                ["message"] = result.Message
            });
        }
    }
}
