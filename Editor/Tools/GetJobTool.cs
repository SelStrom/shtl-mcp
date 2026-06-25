using Newtonsoft.Json.Linq;
using Shtl.Mcp.Jobs;

namespace Shtl.Mcp.Tools
{
    /// Опрос статуса/результата async-job по jobId. Не требует главного потока (читает in-memory
    /// JobStore без Unity API) → опрос работает, даже когда главный поток занят.
    public sealed class GetJobTool : ITool
    {
        readonly JobStore _jobs;

        public GetJobTool(JobStore jobs)
        {
            _jobs = jobs;
        }

        public string Name => "get_job";

        public string Description =>
            "Poll the status and result of an async job by jobId. Returns status (running|done|failed) " +
            "with result (when done) or error (when failed). Unknown jobId returns a structured error.";

        public bool NeedsMainThread => false;

        public JObject InputSchema => new JObject
        {
            ["type"] = "object",
            ["properties"] = new JObject
            {
                ["jobId"] = new JObject
                {
                    ["type"] = "string",
                    ["description"] = "Id returned by an async (⏳) tool."
                }
            },
            ["required"] = new JArray { "jobId" }
        };

        public JObject Invoke(JObject args)
        {
            var id = (string)args["jobId"];
            if (string.IsNullOrEmpty(id))
            {
                return new JObject { ["error"] = "jobId is required" };
            }

            var job = _jobs.Get(id);
            if (job == null)
            {
                return new JObject { ["error"] = "unknown jobId: " + id };
            }

            var o = new JObject
            {
                ["jobId"] = job.Id,
                ["tool"] = job.Tool,
                ["status"] = job.Status
            };
            if (job.Result != null)
            {
                o["result"] = job.Result;
            }
            if (job.Error != null)
            {
                o["error"] = job.Error;
            }
            if (job.Status == "running" && job.Total > 0) // прогресс run_tests (best-effort)
            {
                o["progress"] = new JObject
                {
                    ["completed"] = job.Completed,
                    ["total"] = job.Total,
                    ["currentTest"] = job.CurrentTest
                };
            }
            return o;
        }
    }
}
