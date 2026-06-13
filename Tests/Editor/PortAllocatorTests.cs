using System;
using NUnit.Framework;
using Shtl.Mcp.Common;

namespace Shtl.Mcp.Editor.Tests
{
    public class PortAllocatorTests
    {
        [Test] public void Preferred_IsDeterministicAndInRange()
        {
            int p = PortAllocator.Preferred("/Users/a/PerfectWar");
            Assert.AreEqual(p, PortAllocator.Preferred("/Users/a/PerfectWar"));
            Assert.GreaterOrEqual(p, PortAllocator.Base);
            Assert.Less(p, PortAllocator.Base + PortAllocator.Range);
        }

        [Test] public void Allocate_ReturnsPreferred_WhenFree()
        {
            int pref = PortAllocator.Preferred("/p");
            Assert.AreEqual(pref, PortAllocator.Allocate("/p", _ => true));
        }

        [Test] public void Allocate_FallsBack_WhenPreferredTaken()
        {
            int pref = PortAllocator.Preferred("/p");
            int got = PortAllocator.Allocate("/p", port => port != pref);
            Assert.AreNotEqual(pref, got);
            Assert.GreaterOrEqual(got, PortAllocator.Base);
            Assert.Less(got, PortAllocator.Base + PortAllocator.Range);
        }
    }
}
