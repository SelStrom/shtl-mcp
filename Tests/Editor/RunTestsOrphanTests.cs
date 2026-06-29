using System;
using NUnit.Framework;
using UnityEditor;
using Shtl.Mcp.Jobs;
using Shtl.Mcp.Tools;

namespace Shtl.Mcp.Editor.Tests
{
    /// Orphan-sweep run_tests: зависший/осиротевший прогон должен авто-fail'иться и не блокировать новые.
    ///
    /// SweepOrphan читает live-маркер `Shtl.Mcp.TestJob` (его обязаны использовать как есть) и зовёт
    /// TestRunnerNoThrottle.Restore(). При прогоне через MCP run_tests эти live-ключи держат сам прогон,
    /// поэтому снимаем/возвращаем маркер и no-throttle в OneTime-хуках. JobStore — на ИЗОЛИРОВАННОМ ключе,
    /// поэтому live-job не трогаем.
    public class RunTestsOrphanTests
    {
        const string JobsKey = "Shtl.Mcp.Jobs.Test.RunTestsOrphanTests";

        string _liveMarker;
        TestRunnerNoThrottle.StateSnapshot _liveNoThrottle;

        [OneTimeSetUp]
        public void OneTimeSetUp()
        {
            _liveMarker = SessionState.GetString(RunTestsTool.JobMarkerKey, "");
            // SweepOrphan внутри зовёт TestRunnerNoThrottle.Restore() — снимок страхует live-no-throttle.
            _liveNoThrottle = TestRunnerNoThrottle.Snapshot();
        }

        [OneTimeTearDown]
        public void OneTimeTearDown()
        {
            if (string.IsNullOrEmpty(_liveMarker))
            {
                SessionState.EraseString(RunTestsTool.JobMarkerKey);
            }
            else
            {
                SessionState.SetString(RunTestsTool.JobMarkerKey, _liveMarker);
            }
            TestRunnerNoThrottle.RestoreSnapshot(_liveNoThrottle);
        }

        [SetUp]
        public void SetUp()
        {
            SessionState.EraseString(JobsKey);
            SessionState.EraseString(RunTestsTool.JobMarkerKey);
        }

        [TearDown]
        public void TearDown()
        {
            SessionState.EraseString(JobsKey);
            SessionState.EraseString(RunTestsTool.JobMarkerKey);
        }

        [Test]
        public void Sweep_FailsStaleRunningJob_AndClearsMarker()
        {
            var jobs = new JobStore(JobsKey);
            var id = jobs.Create("run_tests");
            jobs.BackdateForTest(id, (DateTime.UtcNow - TimeSpan.FromMinutes(20)).Ticks); // старше таймаута
            SessionState.SetString(RunTestsTool.JobMarkerKey, id);

            RunTestsTool.SweepOrphan(jobs);

            var job = jobs.Get(id);
            Assert.AreEqual("failed", job.Status);
            StringAssert.Contains("orphan", job.Error);
            Assert.IsEmpty(SessionState.GetString(RunTestsTool.JobMarkerKey, ""), "маркер снят → новые прогоны не блокируются");
        }

        [Test]
        public void Sweep_LeavesFreshRunningJob()
        {
            var jobs = new JobStore(JobsKey);
            var id = jobs.Create("run_tests");
            SessionState.SetString(RunTestsTool.JobMarkerKey, id);

            RunTestsTool.SweepOrphan(jobs);

            Assert.AreEqual("running", jobs.Get(id).Status, "свежий прогон трогать нельзя");
            Assert.AreEqual(id, SessionState.GetString(RunTestsTool.JobMarkerKey, ""));
        }

        // Маркер указывает на исчезнувший job (например, JobStore не восстановился) → подчистить, не зависать.
        [Test]
        public void Sweep_ClearsMarker_WhenJobMissing()
        {
            var jobs = new JobStore(JobsKey);
            SessionState.SetString(RunTestsTool.JobMarkerKey, "ghost-id");

            RunTestsTool.SweepOrphan(jobs);

            Assert.IsEmpty(SessionState.GetString(RunTestsTool.JobMarkerKey, ""));
        }
    }
}
