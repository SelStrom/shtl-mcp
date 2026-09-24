using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using TNode = NUnit.Framework.Interfaces.TNode;
using UnityEditor.TestTools.TestRunner.Api;
using Shtl.Mcp.Tools;

namespace Shtl.Mcp.Editor.Tests
{
    /// Результат run_tests различает все исходы Test Runner'а. Формы деревьев — снятые с реального прогона
    /// (NUnit 3.5): сьют с Passed + Inconclusive детьми и сьют из одного Inconclusive оба имеют статус Passed.
    public class TestRunPayloadTests
    {
        [Test] public void PassedPlusInconclusive_ReportsInconclusive()
        {
            var root = Suite(TestStatus.Passed,
                Leaf("P.Passes", TestStatus.Passed),
                Leaf("P.Inconclusive", TestStatus.Inconclusive, "why"));

            var p = TestRunCallbacks.BuildPayload(root);

            Assert.AreEqual(1, (int)p["passed"]);
            Assert.AreEqual(1, (int)p["inconclusive"]);
            Assert.AreEqual("Inconclusive", (string)p["status"]);
            Assert.AreEqual("P.Inconclusive", (string)p["inconclusiveTests"][0]["name"]);
            Assert.AreEqual("why", (string)p["inconclusiveTests"][0]["message"]);
            Assert.IsEmpty(p["failures"]);
        }

        [Test] public void OnlyInconclusive_IsNotPassed()
        {
            var root = Suite(TestStatus.Passed, Leaf("P.Inconclusive", TestStatus.Inconclusive));

            var p = TestRunCallbacks.BuildPayload(root);

            Assert.AreEqual(0, (int)p["passed"]);
            Assert.AreEqual("Inconclusive", (string)p["status"]);
        }

        [Test] public void FailedWinsOverInconclusive()
        {
            var root = Suite(TestStatus.Failed,
                Leaf("P.Fails", TestStatus.Failed, "boom"),
                Leaf("P.Inconclusive", TestStatus.Inconclusive));

            var p = TestRunCallbacks.BuildPayload(root);

            Assert.AreEqual("Failed", (string)p["status"]);
            Assert.AreEqual("P.Fails", (string)p["failures"][0]["name"]);
            Assert.AreEqual(1, p["inconclusiveTests"].Count());
        }

        [Test] public void SuiteLevelFailure_WithoutFailedLeaves_IsFailed()
        {
            var root = Suite(TestStatus.Failed, Leaf("P.Passes", TestStatus.Passed));

            Assert.AreEqual("Failed", (string)TestRunCallbacks.BuildPayload(root)["status"]);
        }

        [Test] public void IgnoredIsSkipped_AllPassedIsPassed()
        {
            var skipped = Suite(TestStatus.Skipped, Leaf("P.Passes", TestStatus.Passed), Leaf("P.Ignored", TestStatus.Skipped));
            var passed = Suite(TestStatus.Passed, Leaf("P.Passes", TestStatus.Passed));

            Assert.AreEqual("Skipped", (string)TestRunCallbacks.BuildPayload(skipped)["status"]);
            Assert.AreEqual(1, (int)TestRunCallbacks.BuildPayload(skipped)["skipped"]);
            Assert.AreEqual("Passed", (string)TestRunCallbacks.BuildPayload(passed)["status"]);
            Assert.AreEqual(0, (int)TestRunCallbacks.BuildPayload(passed)["inconclusive"]);
        }

        static FakeResult Leaf(string name, TestStatus status, string message = null) =>
            new FakeResult { FullName = name, TestStatus = status, Message = message, Kids = new FakeResult[0] };

        // Счётчики сьюта — сумма по листьям, как их отдаёт ITestResultAdaptor («the test and all its children»).
        static FakeResult Suite(TestStatus status, params FakeResult[] kids) => new FakeResult
        {
            FullName = "P", TestStatus = status, Kids = kids,
            PassCount = kids.Count(k => k.TestStatus == TestStatus.Passed),
            FailCount = kids.Count(k => k.TestStatus == TestStatus.Failed),
            SkipCount = kids.Count(k => k.TestStatus == TestStatus.Skipped),
            InconclusiveCount = kids.Count(k => k.TestStatus == TestStatus.Inconclusive)
        };

        sealed class FakeResult : ITestResultAdaptor
        {
            public FakeResult[] Kids;
            public ITestAdaptor Test => null;
            public string Name => FullName;
            public string FullName { get; set; }
            public string ResultState => TestStatus.ToString();
            public TestStatus TestStatus { get; set; }
            public double Duration => 0;
            public DateTime StartTime => default;
            public DateTime EndTime => default;
            public string Message { get; set; }
            public string StackTrace => null;
            public int AssertCount => 0;
            public int FailCount { get; set; }
            public int PassCount { get; set; }
            public int SkipCount { get; set; }
            public int InconclusiveCount { get; set; }
            public bool HasChildren => Kids.Length > 0;
            public IEnumerable<ITestResultAdaptor> Children => Kids;
            public string Output => null;
            public TNode ToXml() => throw new NotSupportedException();
        }
    }
}
