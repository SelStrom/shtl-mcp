using System.Linq;
using NUnit.Framework;
using ShtlMcp.Logging;

namespace ShtlMcp.Editor.Tests
{
    public class LogBufferTests
    {
        [Test] public void Add_EvictsBeyondCapacity()
        {
            var b = new LogBuffer(2);
            b.Add("a", "", LogLevel.Info);
            b.Add("b", "", LogLevel.Info);
            b.Add("c", "", LogLevel.Info);
            var items = b.Get(null, 10);
            Assert.AreEqual(2, items.Count);
            CollectionAssert.AreEqual(new[] { "b", "c" }, items.Select(i => i.Message).ToArray());
        }

        [Test] public void Get_FiltersByMinLevel()
        {
            var b = new LogBuffer(10);
            b.Add("i", "", LogLevel.Info);
            b.Add("w", "", LogLevel.Warning);
            b.Add("e", "", LogLevel.Error);
            var errs = b.Get(LogLevel.Warning, 10).Select(i => i.Message).ToArray();
            CollectionAssert.AreEqual(new[] { "w", "e" }, errs);
        }

        [Test] public void Get_LimitsCount_ReturningMostRecent()
        {
            var b = new LogBuffer(10);
            b.Add("1", "", LogLevel.Info);
            b.Add("2", "", LogLevel.Info);
            b.Add("3", "", LogLevel.Info);
            CollectionAssert.AreEqual(new[] { "2", "3" }, b.Get(null, 2).Select(i => i.Message).ToArray());
        }
    }
}
