using NUnit.Framework;
using Shtl.Mcp.Lifecycle;

namespace Shtl.Mcp.Editor.Tests
{
    /// Ring-buffer хвоста вызовов (AC5.5): ёмкость, порядок новейшие-первыми, ok-флаг.
    public class CallTailTests
    {
        [Test] public void Empty_Snapshot_IsEmpty()
        {
            Assert.AreEqual(0, new CallTail(5).Snapshot().Length);
        }

        [Test] public void Snapshot_NewestFirst()
        {
            var t = new CallTail(5);
            t.Record("a", true, 1);
            t.Record("b", true, 2);
            t.Record("c", true, 3);
            var s = t.Snapshot();
            Assert.AreEqual(3, s.Length);
            Assert.AreEqual("c", s[0].Method, "новейший — первым");
            Assert.AreEqual("b", s[1].Method);
            Assert.AreEqual("a", s[2].Method);
        }

        [Test] public void Capacity_EvictsOldest()
        {
            var t = new CallTail(2);
            t.Record("a", true, 1);
            t.Record("b", true, 1);
            t.Record("c", true, 1);
            var s = t.Snapshot();
            Assert.AreEqual(2, s.Length);
            Assert.AreEqual("c", s[0].Method);
            Assert.AreEqual("b", s[1].Method, "'a' вытеснен");
        }

        [Test] public void Ok_Flag_And_Ms_Preserved()
        {
            var t = new CallTail(5);
            t.Record("fail", false, 12.5);
            var e = t.Snapshot()[0];
            Assert.IsFalse(e.Ok);
            Assert.AreEqual(12.5, e.Ms, 0.0001);
            Assert.AreEqual("fail", e.Method);
        }

        [Test] public void NullMethod_Normalized()
        {
            var t = new CallTail(5);
            t.Record(null, true, 1);
            Assert.AreEqual("?", t.Snapshot()[0].Method);
        }
    }
}
