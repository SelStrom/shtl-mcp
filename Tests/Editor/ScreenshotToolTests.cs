using NUnit.Framework;
using Newtonsoft.Json.Linq;
using UnityEngine;
using Shtl.Mcp.Tools;

namespace Shtl.Mcp.Editor.Tests
{
    /// M5/AC3.14: screenshot по конкретной камере (параметр camera).
    public class ScreenshotToolTests
    {
        const string Obj = "ShtlM5CamObj";

        [TearDown]
        public void TearDown()
        {
            var go = GameObject.Find(Obj);
            if (go != null)
            {
                Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void Camera_NotFound_Error()
        {
            var res = new ScreenshotTool().Invoke(new JObject { ["camera"] = "NoSuchCamera" });
            StringAssert.Contains("not found", (string)res["error"]);
        }

        [Test]
        public void Target_WithoutCameraComponent_Error()
        {
            new GameObject(Obj);
            var res = new ScreenshotTool().Invoke(new JObject { ["camera"] = Obj });
            StringAssert.Contains("no Camera component", (string)res["error"]);
        }

        [Test]
        public void NamedCamera_ReturnsImage()
        {
            var go = new GameObject(Obj);
            go.AddComponent<Camera>();
            var res = new ScreenshotTool().Invoke(new JObject { ["camera"] = Obj, ["width"] = 64, ["height"] = 64 });
            Assert.IsNull(res["error"], res.ToString());
            var content = (JArray)res["_content"];
            Assert.AreEqual("image", (string)content[0]["type"], "кадр именно как MCP image-content");
            StringAssert.Contains(Obj, (string)content[1]["text"], "подпись указывает камеру");
        }
    }
}
