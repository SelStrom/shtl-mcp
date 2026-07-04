using System;
using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;
using UnityEditor;
using Shtl.Mcp.Jobs;

namespace Shtl.Mcp.Tools
{
    /// Создать/перезаписать текстовый ассет под Assets/ — парный к read_asset (AC3.9). Для компилируемых
    /// расширений (.cs/.asmdef/.asmref) при refresh импорт триггерит компиляцию + domain reload → async-job
    /// (INV-1): jobId сразу, ошибки компиляции — через get_job после reload (reload-job-канал recompile).
    /// Остальные расширения — синхронно. Обычный тул, не footgun (решение человека, F3/AC3.9).
    public sealed class WriteAssetTool : ITool
    {
        static readonly string[] CompiledExt = { ".cs", ".asmdef", ".asmref" };

        readonly JobStore _jobs;
        readonly Func<int> _reloadCount;

        public WriteAssetTool(JobStore jobs, Func<int> reloadCount)
        {
            _jobs = jobs;
            _reloadCount = reloadCount;
        }

        public string Name => "write_asset";

        public string Description =>
            "Create or overwrite a text asset under Assets/ (.cs, .uxml, .uss, .json, .asmdef, ...). Pair of " +
            "read_asset. 'refresh' (default true) imports the file; for compiled extensions (.cs/.asmdef/.asmref) " +
            "that triggers script compilation + domain reload — returns a jobId, poll get_job(jobId) for the " +
            "result (compile errors are delivered there). 'createFolders' (default true) creates missing folders.";

        public bool NeedsMainThread => true;

        public JObject InputSchema => new JObject
        {
            ["type"] = "object",
            ["properties"] = new JObject
            {
                ["path"] = new JObject { ["type"] = "string", ["description"] = "Project-relative path under 'Assets/', e.g. 'Assets/Scripts/Foo.cs'." },
                ["content"] = new JObject { ["type"] = "string", ["description"] = "Full text content to write (UTF-8)." },
                ["refresh"] = new JObject { ["type"] = "boolean", ["description"] = "Import after write (default true). For .cs this compiles the script." },
                ["createFolders"] = new JObject { ["type"] = "boolean", ["description"] = "Create missing parent folders (default true)." }
            },
            ["required"] = new JArray { "path", "content" }
        };

        public JObject Invoke(JObject args)
        {
            var path = ((string)args["path"] ?? "").Replace('\\', '/');
            if (!path.StartsWith("Assets/", StringComparison.Ordinal) || path.EndsWith("/", StringComparison.Ordinal))
            {
                return new JObject { ["error"] = "path must be a file under 'Assets/' (got: '" + path + "'); Packages/ is read-only for write_asset" };
            }
            if (path.Split('/').Any(seg => seg == ".." || seg == "."))
            {
                return new JObject { ["error"] = "path must not contain '.' or '..' segments: " + path };
            }
            var content = (string)args["content"];
            if (content == null)
            {
                return new JObject { ["error"] = "content is required (empty string is allowed)" };
            }
            bool refresh = args["refresh"] == null || (bool)args["refresh"];
            bool createFolders = args["createFolders"] == null || (bool)args["createFolders"];

            var full = Path.GetFullPath(path);
            var dir = Path.GetDirectoryName(full);
            if (!Directory.Exists(dir))
            {
                if (!createFolders)
                {
                    return new JObject { ["error"] = "parent folder does not exist (createFolders=false): " + path };
                }
                try { Directory.CreateDirectory(dir); }
                catch (Exception e) { return new JObject { ["error"] = "could not create folders: " + e.Message }; }
            }

            bool existed = File.Exists(full);
            var ext = Path.GetExtension(path).ToLowerInvariant();
            bool compiled = CompiledExt.Contains(ext);

            // Компилируемый ассет + refresh → импорт вызовет recompile/reload; ответ должен уйти ДО reload
            // (INV-1) → reload-job-канал recompile (как refresh_assets). Begin ДО записи: занятый канал —
            // ошибка без побочных эффектов на диске.
            JObject begin = null;
            if (refresh && compiled)
            {
                begin = ReloadJobs.Begin(_jobs, "recompile", "write_asset", _reloadCount());
                if (begin["error"] != null)
                {
                    return begin;
                }
            }

            try
            {
                File.WriteAllText(full, content);
                if (refresh)
                {
                    AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
                }
            }
            catch (Exception e)
            {
                if (begin != null)
                {
                    ReloadJobs.AbortPending(_jobs, "write_asset failed: " + e.Message);
                }
                return new JObject { ["error"] = "write failed: " + e.Message };
            }

            if (begin != null)
            {
                begin["path"] = path;
                begin["created"] = !existed;
                return begin;
            }
            return new JObject
            {
                ["written"] = true,
                ["path"] = path,
                ["created"] = !existed,
                ["bytes"] = System.Text.Encoding.UTF8.GetByteCount(content)
            };
        }
    }
}
