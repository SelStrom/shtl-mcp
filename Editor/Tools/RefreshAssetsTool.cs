using System;
using Newtonsoft.Json.Linq;
using UnityEditor;
using Shtl.Mcp.Jobs;

namespace Shtl.Mcp.Tools
{
    /// Импорт изменённых на диске ассетов (`AssetDatabase.Refresh`). Async-job: если изменились скрипты —
    /// триггерит domain reload, синхронный ответ умер бы вместе с доменом (INV-1). Делит reload-job-канал
    /// с recompile (та же финализация: reload → done; без reload после grace → done no changes).
    public sealed class RefreshAssetsTool : ITool
    {
        readonly JobStore _jobs;
        readonly Func<int> _reloadCount;

        public RefreshAssetsTool(JobStore jobs, Func<int> reloadCount)
        {
            _jobs = jobs;
            _reloadCount = reloadCount;
        }

        public string Name => "refresh_assets";

        public string Description =>
            "Import assets changed on disk (AssetDatabase.Refresh). Async: returns a jobId; poll " +
            "get_job(jobId). If scripts changed it triggers a domain reload; result is delivered after it.";

        public bool NeedsMainThread => true;

        public JObject InputSchema => new JObject { ["type"] = "object", ["properties"] = new JObject() };

        public JObject Invoke(JObject args)
        {
            var begin = ReloadJobs.Begin(_jobs, "recompile", "refresh_assets", _reloadCount());
            if (begin["error"] != null)
            {
                return begin;
            }
            AssetDatabase.Refresh();
            return begin;
        }
    }
}
