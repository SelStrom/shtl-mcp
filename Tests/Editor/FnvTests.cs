using NUnit.Framework;
using Shtl.Mcp.Common;

namespace Shtl.Mcp.Editor.Tests
{
    public class FnvTests
    {
        [Test] public void Hash32_IsDeterministic()
            => Assert.AreEqual(Fnv.Hash32("/Users/a/PerfectWar"), Fnv.Hash32("/Users/a/PerfectWar"));

        [Test] public void Hash32_DiffersForDifferentInput()
            => Assert.AreNotEqual(Fnv.Hash32("/a"), Fnv.Hash32("/b"));

        [Test] public void Hash4_HasFourHexChars()
        {
            var h = Fnv.Hash4("/Users/a/PerfectWar");
            Assert.AreEqual(4, h.Length);
            StringAssert.IsMatch("^[0-9a-f]{4}$", h);
        }
    }
}
