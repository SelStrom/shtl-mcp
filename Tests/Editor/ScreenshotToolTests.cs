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

        // Политика размера кадра. -1 в aw/ah = «параметр не задан» (null). MaxDim=2048.
        [TestCase(800, 600, true, 1920, 1080, 800, 600, TestName = "both_explicit_as_is")]
        [TestCase(1920, -1, true, 1280, 720, 1920, 1080, TestName = "width_only_derives_height_by_aspect")]
        [TestCase(-1, 720, true, 1280, 720, 1280, 720, TestName = "height_only_derives_width_by_aspect")]
        [TestCase(-1, -1, true, 1600, 900, 1600, 900, TestName = "no_args_uses_native")]
        [TestCase(-1, -1, false, 0, 0, 1024, 576, TestName = "no_native_falls_back_1024x576")]
        [TestCase(1024, -1, false, 0, 0, 1024, 576, TestName = "width_only_no_native_uses_16by9")]
        [TestCase(-1, -1, true, 10000, 5000, 2048, 2048, TestName = "clamps_to_max")]
        [TestCase(5, 5, true, 1920, 1080, 16, 16, TestName = "clamps_to_min")]
        public void ComputeTargetSize_Cases(int aw, int ah, bool native, int nw, int nh, int ew, int eh)
        {
            int? argW = aw < 0 ? (int?)null : aw;
            int? argH = ah < 0 ? (int?)null : ah;
            ScreenshotTool.ComputeTargetSize(argW, argH, native, nw, nh, out int w, out int h);
            Assert.AreEqual(ew, w, "width");
            Assert.AreEqual(eh, h, "height");
        }
    }
}
