using NUnit.Framework;
using Shtl.Mcp.Common;

namespace Shtl.Mcp.Editor.Tests
{
    public class ServerNameTests
    {
        [Test] public void Base_WhenNoCollision()
            => Assert.AreEqual("unity-perfectwar",
                 ServerName.Resolve("PerfectWar", "/p", _ => null));

        [Test] public void Base_WhenSamePathAlreadyLive()
            => Assert.AreEqual("unity-perfectwar",
                 ServerName.Resolve("PerfectWar", "/p", _ => "/p"));

        [Test] public void Suffixed_WhenNameCollidesWithDifferentPath()
        {
            string name = ServerName.Resolve("PerfectWar", "/worktree", _ => "/main");
            StringAssert.StartsWith("unity-perfectwar-", name);
            Assert.AreEqual("unity-perfectwar-" + Fnv.Hash4("/worktree"), name);
        }

        [Test] public void Sanitize_LowercasesAndDashes()
            => Assert.AreEqual("unity-my-game", ServerName.Resolve("My Game!", "/p", _ => null));

        [Test] public void AssignedName_WinsOverFreeBaseName()
            => Assert.AreEqual("unity-perfectwar-worktree",
                 ServerName.Resolve("PerfectWar", "/w/worktree", _ => null, "unity-perfectwar-worktree"));

        [Test] public void AssignedName_WinsOverCollision()
            => Assert.AreEqual("unity-perfectwar",
                 ServerName.Resolve("PerfectWar", "/main", _ => "/other", "unity-perfectwar"));

        [Test] public void FolderName_WhenBaseTakenAndFolderFree()
            => Assert.AreEqual("unity-perfectwar-second",
                 ServerName.Resolve("PerfectWar", "/w/perfectwar-second",
                     name => name == "unity-perfectwar" ? "/w/perfectwar" : null));

        [Test] public void HashSuffix_WhenFolderNameEqualsBaseName()
        {
            // клон в папке с именем продукта: имя папки не различает — остаётся хеш пути
            string name = ServerName.Resolve("PerfectWar", "/w/perfectwar",
                n => n == "unity-perfectwar" ? "/main/perfectwar" : null);
            Assert.AreEqual("unity-perfectwar-" + Fnv.Hash4("/w/perfectwar"), name);
        }

        [Test] public void HashSuffix_WhenFolderNameAlsoTaken()
        {
            string name = ServerName.Resolve("PerfectWar", "/w/second", _ => "/somebody-else");
            Assert.AreEqual("unity-perfectwar-" + Fnv.Hash4("/w/second"), name);
        }

        [Test] public void FolderName_IgnoresTrailingSeparator()
            => Assert.AreEqual("unity-perfectwar-second",
                 ServerName.Resolve("PerfectWar", "/w/perfectwar-second/",
                     name => name == "unity-perfectwar" ? "/w/perfectwar" : null));
    }
}
