using System;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.Compilation;
using Newtonsoft.Json.Linq;
using Shtl.Mcp.Jobs;
using Shtl.Mcp.Tools;

namespace Shtl.Mcp.Editor.Tests
{
    /// Финализация reload-job (recompile/set_play_mode): reload-detect, grace-таймаут, ошибки компиляции,
    /// достижение play-режима. Маркер `Shtl.Mcp.ReloadJob` — live-ключ; снимаем/возвращаем в OneTime.
    public class ReloadJobsTests
    {
        const string JobsKey = "Shtl.Mcp.Jobs.Test.ReloadJobsTests";
        const string CompileErrKey = "Shtl.Mcp.ReloadJob.CompileErr";
        string _liveMarker;

        [OneTimeSetUp]
        public void OneTimeSetUp()
        {
            _liveMarker = SessionState.GetString(ReloadJobs.MarkerKey, "");
        }

        [OneTimeTearDown]
        public void OneTimeTearDown()
        {
            if (string.IsNullOrEmpty(_liveMarker))
            {
                SessionState.EraseString(ReloadJobs.MarkerKey);
            }
            else
            {
                SessionState.SetString(ReloadJobs.MarkerKey, _liveMarker);
            }
        }

        [SetUp]
        public void SetUp()
        {
            SessionState.EraseString(JobsKey);
            SessionState.EraseString(ReloadJobs.MarkerKey);
            SessionState.EraseString(CompileErrKey);
        }

        [TearDown]
        public void TearDown()
        {
            SetUp();
        }

        // startedTicks маркера в прошлое — эмуляция истёкшего grace без реального ожидания.
        static void AgeMarker(TimeSpan back)
        {
            var m = JObject.Parse(SessionState.GetString(ReloadJobs.MarkerKey, ""));
            m["startedTicks"] = (DateTime.UtcNow - back).Ticks;
            SessionState.SetString(ReloadJobs.MarkerKey, m.ToString(Newtonsoft.Json.Formatting.None));
        }

        static string MarkerJobId()
        {
            return (string)JObject.Parse(SessionState.GetString(ReloadJobs.MarkerKey, ""))["jobId"];
        }

        [Test]
        public void Begin_SecondReloadJob_Blocked()
        {
            var jobs = new JobStore(JobsKey);
            var first = ReloadJobs.Begin(jobs, "recompile", "recompile", 0);
            Assert.IsNull(first["error"]);
            var second = ReloadJobs.Begin(jobs, "recompile", "recompile", 0);
            StringAssert.Contains("in progress", (string)second["error"]);
        }

        [Test]
        public void Recompile_ReloadHappened_CompletesDone()
        {
            var jobs = new JobStore(JobsKey);
            ReloadJobs.Begin(jobs, "recompile", "recompile", 5);
            var id = MarkerJobId();

            ReloadJobs.FinalizeOnTick(jobs, 6); // reloadCount вырос → reload случился

            var job = jobs.Get(id);
            Assert.AreEqual("done", job.Status);
            StringAssert.Contains("recompiled", job.Result);
            Assert.IsEmpty(SessionState.GetString(ReloadJobs.MarkerKey, ""), "маркер снят");
        }

        [Test]
        public void Recompile_NoReload_BeforeGrace_StaysRunning()
        {
            var jobs = new JobStore(JobsKey);
            ReloadJobs.Begin(jobs, "recompile", "recompile", 5);
            var id = MarkerJobId();

            ReloadJobs.FinalizeOnTick(jobs, 5); // reload не случился, grace не истёк

            Assert.AreEqual("running", jobs.Get(id).Status, "до grace job висит running");
        }

        [Test]
        public void Recompile_NoReload_AfterGrace_NoErrors_DoneNoChanges()
        {
            var jobs = new JobStore(JobsKey);
            ReloadJobs.Begin(jobs, "recompile", "recompile", 5);
            var id = MarkerJobId();
            AgeMarker(TimeSpan.FromSeconds(10));

            ReloadJobs.FinalizeOnTick(jobs, 5);

            var job = jobs.Get(id);
            Assert.AreEqual("done", job.Status);
            StringAssert.Contains("no changes", job.Result);
        }

        [Test]
        public void Recompile_NoReload_AfterGrace_CompileErrors_Fails()
        {
            var jobs = new JobStore(JobsKey);
            ReloadJobs.Begin(jobs, "recompile", "recompile", 5);
            var id = MarkerJobId();
            ReloadJobs.OnAssemblyCompiled("Asm.dll", new[]
            {
                new CompilerMessage { message = "CS9999 boom", type = CompilerMessageType.Error }
            });
            AgeMarker(TimeSpan.FromSeconds(10));

            ReloadJobs.FinalizeOnTick(jobs, 5);

            var job = jobs.Get(id);
            Assert.AreEqual("failed", job.Status);
            StringAssert.Contains("boom", job.Error);
        }

        [Test]
        public void SetPlayMode_TargetReached_CompletesJob()
        {
            var jobs = new JobStore(JobsKey);
            ReloadJobs.Begin(jobs, "set_play_mode", "set_play_mode", 5, "play");
            var id = MarkerJobId();

            ReloadJobs.OnPlayModeChanged(jobs, PlayModeStateChange.EnteredPlayMode);

            var job = jobs.Get(id);
            Assert.AreEqual("done", job.Status);
            StringAssert.Contains("play", job.Result);
            Assert.IsEmpty(SessionState.GetString(ReloadJobs.MarkerKey, ""));
        }

        [Test]
        public void SetPlayMode_WrongState_StaysRunning()
        {
            var jobs = new JobStore(JobsKey);
            ReloadJobs.Begin(jobs, "set_play_mode", "set_play_mode", 5, "play");
            var id = MarkerJobId();

            ReloadJobs.OnPlayModeChanged(jobs, PlayModeStateChange.EnteredEditMode); // не target

            Assert.AreEqual("running", jobs.Get(id).Status);
        }
    }
}
