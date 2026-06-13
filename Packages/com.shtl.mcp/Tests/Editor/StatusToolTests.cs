using NUnit.Framework;
using Newtonsoft.Json.Linq;
using ShtlMcp.Tools;

namespace ShtlMcp.Editor.Tests
{
    class FakeContext : IEditorContext
    {
        public string ProjectName => "PerfectWar";
        public string ProjectPath => "/p";
        public string UnityVersion => "2022.3.40f1";
        public string ServerName => "unity-perfectwar";
        public int Port => 9712;
        public int Pid => 4242;
        public bool IsPlaying => false;
        public bool IsCompiling => false;
        public double UptimeSeconds => 75;
        public int ClientCount => 1;
    }

    public class StatusToolTests
    {
        [Test] public void Invoke_ReturnsIdentityAndMode()
        {
            var o = new StatusTool(new FakeContext()).Invoke(new JObject());
            Assert.AreEqual("PerfectWar", (string)o["projectName"]);
            Assert.AreEqual("unity-perfectwar", (string)o["serverName"]);
            Assert.AreEqual(9712, (int)o["port"]);
            Assert.AreEqual("edit", (string)o["mode"]);
            Assert.AreEqual("ok", (string)o["health"]);
        }

        [Test] public void Schema_IsObjectType()
            => Assert.AreEqual("object", (string)new StatusTool(new FakeContext()).InputSchema["type"]);
    }
}
