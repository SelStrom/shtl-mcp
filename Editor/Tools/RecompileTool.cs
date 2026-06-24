using System;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEditor.Compilation;
using Shtl.Mcp.Jobs;

namespace Shtl.Mcp.Tools
{
    /// Импорт изменённых ассетов + перекомпиляция скриптов (триггерит domain reload). Async-job (INV-1):
    /// сразу отдаёт jobId, результат — через get_job ПОСЛЕ reload (`status: recompiled` при успехе, либо
    /// ошибки компиляции, либо `no changes`). См. ReloadJobs (reload-spanning финализация).
    public sealed class RecompileTool : ITool
    {
        readonly JobStore _jobs;
        readonly Func<int> _reloadCount;

        public RecompileTool(JobStore jobs, Func<int> reloadCount)
        {
            _jobs = jobs;
            _reloadCount = reloadCount;
        }

        public string Name => "recompile";

        public string Description =>
            "Import changed assets and recompile scripts (triggers a domain reload). Async: returns a jobId " +
            "immediately; poll get_job(jobId) — the result is delivered AFTER the reload (status 'recompiled', " +
            "'no changes', or failed with compile errors). force=true recompiles even with no source changes.";

        public bool NeedsMainThread => true; // AssetDatabase/CompilationPipeline — только главный поток

        public JObject InputSchema => new JObject
        {
            ["type"] = "object",
            ["properties"] = new JObject
            {
                ["force"] = new JObject
                {
                    ["type"] = "boolean",
                    ["description"] = "Force a full recompile + domain reload even with no source changes (default false)."
                }
            }
        };

        public JObject Invoke(JObject args)
        {
            bool force = args["force"] != null && (bool)args["force"];

            var begin = ReloadJobs.Begin(_jobs, "recompile", "recompile", _reloadCount());
            if (begin["error"] != null)
            {
                return begin;
            }

            // Refresh импортирует изменённые скрипты и компилирует затронутое (инкрементально); force —
            // полная перекомпиляция + reload даже без изменений. Reload произойдёт async на след. тике —
            // ответ (jobId) уже ушёл; финализация job — после reload (ReloadJobs.FinalizeOnTick).
            try
            {
                AssetDatabase.Refresh();
                if (force)
                {
                    CompilationPipeline.RequestScriptCompilation();
                }
            }
            catch (Exception e)
            {
                ReloadJobs.AbortPending(_jobs, "recompile failed: " + e.Message);
                return new JObject { ["error"] = "recompile failed: " + e.Message };
            }

            begin["force"] = force;
            return begin;
        }
    }
}
