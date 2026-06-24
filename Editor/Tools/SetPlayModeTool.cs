using System;
using Newtonsoft.Json.Linq;
using UnityEditor;
using Shtl.Mcp.Jobs;

namespace Shtl.Mcp.Tools
{
    /// Вход/выход из Play mode. Async-job (INV-1): вход в Play при Reload Domain ON триггерит domain
    /// reload, синхронный ответ умер бы вместе с доменом. Сразу отдаёт jobId; job завершается по
    /// достижении целевого режима (`playModeStateChanged`, см. ReloadJobs.OnPlayModeChanged).
    public sealed class SetPlayModeTool : ITool
    {
        readonly JobStore _jobs;
        readonly Func<int> _reloadCount;

        public SetPlayModeTool(JobStore jobs, Func<int> reloadCount)
        {
            _jobs = jobs;
            _reloadCount = reloadCount;
        }

        public string Name => "set_play_mode";

        public string Description =>
            "Enter or exit Play mode (state: 'play' | 'edit'). Async: returns a jobId immediately; poll " +
            "get_job(jobId) — done when the target mode is reached (entering Play may trigger a domain reload).";

        public bool NeedsMainThread => true;

        public JObject InputSchema => new JObject
        {
            ["type"] = "object",
            ["properties"] = new JObject
            {
                ["state"] = new JObject
                {
                    ["type"] = "string",
                    ["description"] = "'play' to enter Play mode, 'edit' to exit to Edit mode."
                }
            },
            ["required"] = new JArray { "state" }
        };

        public JObject Invoke(JObject args)
        {
            var state = (string)args["state"];
            if (state != "play" && state != "edit")
            {
                return new JObject { ["error"] = "state must be 'play' or 'edit'" };
            }

            bool wantPlay = state == "play";
            if (EditorApplication.isPlaying == wantPlay)
            {
                // уже в целевом режиме — мгновенный done-job (без перехода/reload)
                var id = _jobs.Create("set_play_mode");
                _jobs.Complete(id, new JObject { ["mode"] = state, ["status"] = "already in target" }.ToString());
                return new JObject { ["jobId"] = id, ["status"] = "done", ["mode"] = state };
            }

            var begin = ReloadJobs.Begin(_jobs, "set_play_mode", "set_play_mode", _reloadCount(), state);
            if (begin["error"] != null)
            {
                return begin;
            }

            try
            {
                if (wantPlay)
                {
                    EditorApplication.EnterPlaymode();
                }
                else
                {
                    EditorApplication.ExitPlaymode();
                }
            }
            catch (Exception e)
            {
                // переход отклонён/бросил → снять маркер сразу, иначе reload-канал залочен до backstop-таймаута
                ReloadJobs.AbortPending(_jobs, "set_play_mode failed: " + e.Message);
                return new JObject { ["error"] = "set_play_mode failed: " + e.Message };
            }
            return begin;
        }
    }
}
