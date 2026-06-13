using System;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;
using NUnit.Framework;
using Shtl.Mcp.Dispatch;

namespace Shtl.Mcp.Editor.Tests
{
    public class MainThreadDispatcherTests
    {
        [Test] public void Drain_ExecutesFifo()
        {
            var d = new MainThreadDispatcher();
            var log = new List<int>();
            d.Enqueue(() => log.Add(1));
            d.Enqueue(() => log.Add(2));
            d.Drain();
            CollectionAssert.AreEqual(new[] { 1, 2 }, log);
        }

        [Test] public void RunOnMain_ReturnsResult_WhenDrained()
        {
            var d = new MainThreadDispatcher();
            int result = 0;
            var t = Task.Run(() => result = d.RunOnMain(() => 42, 2000));
            while (!t.IsCompleted) { d.Drain(); Thread.Sleep(1); }
            d.Drain(); t.Wait();
            Assert.AreEqual(42, result);
        }

        [Test] public void RunOnMain_Throws_WhenNotDrained()
        {
            var d = new MainThreadDispatcher();
            Assert.Throws<TimeoutException>(() => d.RunOnMain(() => 1, 50));
        }

        [Test] public void RunOnMain_PropagatesException()
        {
            var d = new MainThreadDispatcher();
            var t = Task.Run(() => Assert.Throws<InvalidOperationException>(
                () => d.RunOnMain<int>(() => throw new InvalidOperationException("boom"), 2000)));
            while (!t.IsCompleted) { d.Drain(); Thread.Sleep(1); }
            t.Wait();
        }
    }
}
