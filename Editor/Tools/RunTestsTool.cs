using System;
using UnityEditor;
using UnityEngine;
using UnityEditor.TestTools.TestRunner.Api;
using Newtonsoft.Json.Linq;
using Shtl.Mcp.Jobs;

namespace Shtl.Mcp.Tools
{
    /// Прогон EditMode/PlayMode тестов через Unity Test Runner. Async: сразу отдаёт jobId, результат —
    /// через get_job(jobId). Переживает domain reload (durable job + маркер in-flight в SessionState +
    /// переподписка колбэков после reload, см. TestRunCallbacks). Паттерн — CoplayDev/unity-mcp.
    public sealed class RunTestsTool : ITool
    {
        public const string JobMarkerKey = "Shtl.Mcp.TestJob";

        // Грубый safety net: прогон без завершения (RunFinished потерян — краш в середине reload,
        // зависший тест) дольше таймаута → job failed, маркер снят, троттлинг возвращён. Wall-clock от
        // старта (полный no-progress-стриминг отложен, см. TASK.md). Запас большой — наши прогоны секундны.
        static readonly TimeSpan OrphanTimeout = TimeSpan.FromMinutes(10);

        readonly JobStore _jobs;
        static TestRunnerApi _api; // держим живым на время прогона

        public RunTestsTool(JobStore jobs)
        {
            _jobs = jobs;
        }

        public string Name => "run_tests";

        public string Description =>
            "Run EditMode or PlayMode tests via the Unity Test Runner. Async: returns a jobId immediately; " +
            "poll get_job(jobId) for results (passed/failed/skipped counts + failures). Survives domain reload.";

        public bool NeedsMainThread => true;

        public JObject InputSchema => new JObject
        {
            ["type"] = "object",
            ["properties"] = new JObject
            {
                ["mode"] = new JObject { ["type"] = "string", ["description"] = "EditMode (default) or PlayMode." },
                ["filter"] = new JObject { ["type"] = "string", ["description"] = "Optional regex over full test names (Test Runner groupNames)." },
                ["assembly"] = new JObject { ["type"] = "string", ["description"] = "Optional test assembly name to limit the run, e.g. Shtl.Mcp.Editor.Tests." }
            }
        };

        public JObject Invoke(JObject args)
        {
            // запрет параллельных прогонов (один in-flight маркер)
            if (!string.IsNullOrEmpty(SessionState.GetString(JobMarkerKey, "")))
            {
                return new JObject { ["error"] = "a test run is already in progress" };
            }

            var mode = (string)args["mode"] ?? "EditMode";
            var filter = (string)args["filter"];
            var assembly = (string)args["assembly"];

            var f = new Filter
            {
                testMode = mode == "PlayMode" ? TestMode.PlayMode : TestMode.EditMode
            };
            if (!string.IsNullOrEmpty(filter))
            {
                f.groupNames = new[] { filter };
            }
            if (!string.IsNullOrEmpty(assembly))
            {
                f.assemblyNames = new[] { assembly };
            }

            var jobId = _jobs.Create("run_tests");
            SessionState.SetString(JobMarkerKey, jobId);

            // Снять focus-throttle ДО старта: в фоне RunStarted может не успеть тикнуть, и прогон
            // (вместе с сервером) зависнет в throttled-update. RunStarted применит повторно (идемпотентно).
            TestRunnerNoThrottle.Apply();

            if (_api != null)
            {
                UnityEngine.Object.DestroyImmediate(_api); // снять колбэки прошлого прогона (guard дублей ICallbacks)
            }
            _api = ScriptableObject.CreateInstance<TestRunnerApi>();
            _api.hideFlags = HideFlags.HideAndDontSave;
            _api.RegisterCallbacks(new TestRunCallbacks(_jobs));
            _api.Execute(new ExecutionSettings(f));

            EditorApplication.QueuePlayerLoopUpdate(); // один толчок цикла, чтобы прогон стартовал в фоне

            return new JObject
            {
                ["jobId"] = jobId,
                ["status"] = "running",
                ["note"] = "Poll get_job(jobId) for results."
            };
        }

        /// Safety net против зависших/осиротевших прогонов. Вызывается с тика watchdog (главный поток).
        /// Снимает маркер и возвращает троттлинг, если in-flight job исчез/финализирован/перерос таймаут.
        public static void SweepOrphan(JobStore jobs)
        {
            var jobId = SessionState.GetString(JobMarkerKey, "");
            if (string.IsNullOrEmpty(jobId))
            {
                return;
            }
            if (EditorApplication.isCompiling)
            {
                return; // компиляция/reload — это не наш таймаут
            }

            var job = jobs.Get(jobId);
            if (job == null || job.Status != "running")
            {
                // job исчез или уже финализирован, а маркер завис — подчистить, чтобы не блокировать новые прогоны
                SessionState.EraseString(JobMarkerKey);
                TestRunnerNoThrottle.Restore();
                return;
            }

            var age = DateTime.UtcNow - new DateTime(job.StartedAtTicks, DateTimeKind.Utc);
            if (age > OrphanTimeout)
            {
                jobs.Fail(jobId, $"test run orphaned: no completion within {OrphanTimeout.TotalMinutes:0} min");
                SessionState.EraseString(JobMarkerKey);
                TestRunnerNoThrottle.Restore();
            }
        }
    }
}
