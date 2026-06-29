using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
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
                ["assembly"] = new JObject { ["type"] = "string", ["description"] = "Optional test assembly name to limit the run, e.g. Shtl.Mcp.Editor.Tests." },
                ["scenePolicy"] = new JObject { ["type"] = "string", ["description"] = "PlayMode only, for an unsaved active scene: discard (default, revert from disk) | save (write to disk) | abort. Modal-free — never opens a blocking 'Save scene?' dialog. Check sceneDirty via get_hierarchy to decide." }
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

            // PlayMode + грязная сцена → Unity Test Runner показал бы блокирующий «Save scene?» модал, который
            // вешает MCP (главный поток в modal-loop). Решаем программно по политике (AC4.9 modal-free).
            if (mode == "PlayMode")
            {
                var dirtyError = ResolveDirtyScenesForPlayMode((string)args["scenePolicy"]);
                if (dirtyError != null)
                {
                    return new JObject { ["error"] = dirtyError };
                }
            }

            var jobId = _jobs.Create("run_tests");
            SessionState.SetString(JobMarkerKey, jobId);

            // Снять focus-throttle ДО старта: в фоне RunStarted может не успеть тикнуть, и прогон
            // (вместе с сервером) зависнет в throttled-update. RunStarted применит повторно (идемпотентно).
            TestRunnerNoThrottle.Apply();

            // PlayMode-прогон: форсировать DisableDomainReload, иначе вход в Play выгрузит домен и убьёт
            // listener/прогон. Restore — в RunFinished (идемпотентно: для EditMode guard не применялся).
            if (mode == "PlayMode")
            {
                PlayModeOptionsGuard.Apply();
            }

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

        /// Готовит загруженные сцены к PlayMode-прогону без промптящего модала Test Runner'а (AC4.9).
        /// Imperative shell: собирает грязные сцены и выполняет save/discard; ветвление политики — в чистом
        /// DecideScenePolicy (тестируемо без Unity-сцены). Возвращает текст ошибки (отмена прогона) или null.
        static string ResolveDirtyScenesForPlayMode(string scenePolicyArg)
        {
            var dirty = new System.Collections.Generic.List<UnityEngine.SceneManagement.Scene>();
            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                var s = SceneManager.GetSceneAt(i);
                if (s.isLoaded && s.isDirty)
                {
                    dirty.Add(s);
                }
            }

            var scene = dirty.Count > 0 ? dirty[0] : default;
            var error = DecideScenePolicy(dirty.Count, !string.IsNullOrEmpty(scene.path), scenePolicyArg,
                out bool doSave, out bool doDiscard);
            if (error != null)
            {
                return error;
            }
            try
            {
                if (doSave)
                {
                    EditorSceneManager.SaveScene(scene); // молча по существующему пути → без модала
                }
                else if (doDiscard)
                {
                    EditorSceneManager.OpenScene(scene.path, OpenSceneMode.Single); // discard: перечитать с диска
                }
            }
            catch (Exception e)
            {
                return $"PlayMode run aborted: failed to apply scenePolicy on '{scene.path}': {e.Message}";
            }
            return null;
        }

        /// Чистое решение по dirty-scene политике (AC4.9): discard (дефолт, перечитать с диска) | save | abort.
        /// Возвращает текст ошибки (прогон отменить) либо null + флаг действия. doSave/doDiscard — что сделать с
        /// одиночной грязной сценой; оба false при «ошибка» и при «грязных сцен нет». Тестируется без Unity-сцены.
        internal static string DecideScenePolicy(int dirtyCount, bool firstHasPath, string scenePolicyArg,
            out bool doSave, out bool doDiscard)
        {
            doSave = false;
            doDiscard = false;
            if (dirtyCount == 0)
            {
                return null; // сцены чисты — модала не будет, ничего делать не надо
            }

            var policy = string.IsNullOrEmpty(scenePolicyArg) ? "discard" : scenePolicyArg.ToLowerInvariant();
            if (policy == "abort")
            {
                return $"PlayMode run aborted (scenePolicy=abort): {dirtyCount} loaded scene(s) have unsaved " +
                    "changes. Resolve them (save_scene / open_scene) or pass scenePolicy='save'|'discard'.";
            }
            if (policy != "save" && policy != "discard")
            {
                return $"PlayMode run aborted: unknown scenePolicy '{scenePolicyArg}' (expected save|discard|abort).";
            }
            if (dirtyCount > 1)
            {
                return "PlayMode run aborted: multiple loaded scenes have unsaved changes — resolve them via " +
                    "save_scene/open_scene, or pass scenePolicy='abort'. (auto save/discard supports one scene)";
            }
            if (!firstHasPath)
            {
                return $"PlayMode run aborted: the active scene is untitled with unsaved changes — can't {policy} " +
                    "it silently. save_scene with a path first, then re-run.";
            }

            doSave = policy == "save";
            doDiscard = policy == "discard";
            return null;
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
                PlayModeOptionsGuard.Restore(); // идемпотентно: для EditMode-прогона guard не применялся
                return;
            }

            var age = DateTime.UtcNow - new DateTime(job.StartedAtTicks, DateTimeKind.Utc);
            if (age > OrphanTimeout)
            {
                jobs.Fail(jobId, $"test run orphaned: no completion within {OrphanTimeout.TotalMinutes:0} min");
                SessionState.EraseString(JobMarkerKey);
                TestRunnerNoThrottle.Restore();
                PlayModeOptionsGuard.Restore();
            }
        }
    }
}
