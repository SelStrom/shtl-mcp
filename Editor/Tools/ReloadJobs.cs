using System;
using UnityEditor;
using UnityEditor.Compilation;
using Newtonsoft.Json.Linq;
using Shtl.Mcp.Jobs;

namespace Shtl.Mcp.Tools
{
    /// Async-job для команд, которые сами триггерят domain reload (`recompile`, `set_play_mode`). Job +
    /// durable-маркер в SessionState переживают reload, вызванный самой командой; финализация — ПОСЛЕ
    /// reload (синхронный ответ умер бы вместе с доменом, INV-1). Обобщение reload-spanning паттерна
    /// run_tests на не-тест-команды.
    ///
    /// Маркер `Shtl.Mcp.ReloadJob` = {jobId, kind, reloadCount, startedTicks, target}. Финализация:
    ///  - recompile: reloadCount вырос → done(reloaded); иначе после grace без reload — fail (ошибки
    ///    компиляции из CompilationPipeline) либо done(no changes);
    ///  - set_play_mode: по `playModeStateChanged` достигнут target (backstop-таймаут на тике).
    public static class ReloadJobs
    {
        public const string MarkerKey = "Shtl.Mcp.ReloadJob";
        const string CompileErrKey = "Shtl.Mcp.ReloadJob.CompileErr";

        static readonly TimeSpan RecompileGrace = TimeSpan.FromSeconds(5);   // ждём старта компиляции/reload
        static readonly TimeSpan PlayModeTimeout = TimeSpan.FromSeconds(30); // backstop, если переход не случился

        /// Создать reload-job и поставить маркер. Возвращает {jobId,status} или ошибку, если reload-job уже в полёте.
        public static JObject Begin(JobStore jobs, string kind, string tool, int reloadCount, string target = null)
        {
            if (!string.IsNullOrEmpty(SessionState.GetString(MarkerKey, "")))
            {
                return new JObject { ["error"] = "a reload job (recompile/set_play_mode) is already in progress" };
            }

            var jobId = jobs.Create(tool);
            var marker = new JObject
            {
                ["jobId"] = jobId,
                ["kind"] = kind,
                ["reloadCount"] = reloadCount,
                ["startedTicks"] = DateTime.UtcNow.Ticks,
                ["target"] = target
            };
            SessionState.SetString(MarkerKey, marker.ToString(Newtonsoft.Json.Formatting.None));
            SessionState.EraseString(CompileErrKey);

            return new JObject
            {
                ["jobId"] = jobId,
                ["status"] = "running",
                ["note"] = "Poll get_job(jobId); result is delivered after the domain reload."
            };
        }

        /// Копит ошибки компиляции (durable) — подписка `CompilationPipeline.assemblyCompilationFinished`.
        public static void OnAssemblyCompiled(string assemblyPath, CompilerMessage[] messages)
        {
            if (string.IsNullOrEmpty(SessionState.GetString(MarkerKey, "")))
            {
                return; // нет in-flight reload-job — не копим
            }
            foreach (var m in messages)
            {
                if (m.type == CompilerMessageType.Error)
                {
                    var prev = SessionState.GetString(CompileErrKey, "");
                    SessionState.SetString(CompileErrKey, prev + m.message + "\n");
                }
            }
        }

        /// Финализация reload-job с тика watchdog (главный поток). reloadCount — текущий durable-счётчик.
        public static void FinalizeOnTick(JobStore jobs, int reloadCount)
        {
            var raw = SessionState.GetString(MarkerKey, "");
            if (string.IsNullOrEmpty(raw))
            {
                return;
            }
            if (EditorApplication.isCompiling || EditorApplication.isUpdating)
            {
                return; // компиляция/импорт идут — ждём (иначе ложный «no changes» до старта компиляции)
            }

            JObject m;
            try { m = JObject.Parse(raw); }
            catch { SessionState.EraseString(MarkerKey); return; } // битый маркер — снять

            var jobId = (string)m["jobId"];
            var kind = (string)m["kind"];
            var startedTicks = (long?)m["startedTicks"] ?? 0;
            var elapsed = DateTime.UtcNow - new DateTime(startedTicks, DateTimeKind.Utc);

            if (kind == "recompile")
            {
                int atStart = (int?)m["reloadCount"] ?? 0;
                if (reloadCount > atStart)
                {
                    jobs.Complete(jobId, new JObject { ["reloaded"] = true, ["status"] = "recompiled" }.ToString());
                    Clear();
                    return;
                }
                if (elapsed < RecompileGrace)
                {
                    return; // компиляция/reload могут вот-вот начаться
                }
                var err = SessionState.GetString(CompileErrKey, "");
                if (!string.IsNullOrEmpty(err))
                {
                    jobs.Fail(jobId, "compile errors:\n" + err);
                }
                else
                {
                    jobs.Complete(jobId, new JObject
                    {
                        ["reloaded"] = false,
                        ["status"] = "no changes",
                        ["note"] = "Refresh imported nothing to recompile."
                    }.ToString());
                }
                Clear();
                return;
            }

            if (kind == "set_play_mode")
            {
                // Нормальный путь — OnPlayModeChanged; здесь только backstop по таймауту.
                if (elapsed > PlayModeTimeout)
                {
                    jobs.Fail(jobId, "set_play_mode timed out (target=" + (string)m["target"] + ")");
                    Clear();
                }
            }
        }

        /// Завершение set_play_mode по достижению целевого режима — подписка `playModeStateChanged`.
        public static void OnPlayModeChanged(JobStore jobs, PlayModeStateChange change)
        {
            var raw = SessionState.GetString(MarkerKey, "");
            if (string.IsNullOrEmpty(raw))
            {
                return;
            }
            JObject m;
            try { m = JObject.Parse(raw); }
            catch { return; }
            if ((string)m["kind"] != "set_play_mode")
            {
                return;
            }

            var target = (string)m["target"];
            bool reached = (target == "play" && change == PlayModeStateChange.EnteredPlayMode)
                        || (target == "edit" && change == PlayModeStateChange.EnteredEditMode);
            if (reached)
            {
                jobs.Complete((string)m["jobId"],
                    new JObject { ["mode"] = target, ["status"] = "ok" }.ToString());
                Clear();
            }
        }

        /// Немедленно провалить in-flight reload-job и снять маркер (триггер reload-команды бросил до reload
        /// → иначе job завис бы до backstop-таймаута, блокируя канал). Вызывается синхронно из тула.
        public static void AbortPending(JobStore jobs, string error)
        {
            var raw = SessionState.GetString(MarkerKey, "");
            if (string.IsNullOrEmpty(raw))
            {
                return;
            }
            try
            {
                jobs.Fail((string)JObject.Parse(raw)["jobId"], error);
            }
            catch
            {
                // битый маркер — всё равно чистим ниже
            }
            Clear();
        }

        static void Clear()
        {
            SessionState.EraseString(MarkerKey);
            SessionState.EraseString(CompileErrKey);
        }
    }
}
