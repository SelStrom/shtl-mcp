using NUnit.Framework;
using Newtonsoft.Json.Linq;
using Shtl.Mcp.Tools;

namespace Shtl.Mcp.Editor.Tests
{
    /// screenshot_uxml: контрактные guard'ы входа. Happy-path (реальный offscreen-рендер UXML в PNG)
    /// проверяется интеграционно через MCP-вызов в проекте с темой и UXML — здесь тестовой среде их нет.
    public class ScreenshotUxmlToolTests
    {
        [Test]
        public void Uxml_Missing_Error()
        {
            var res = new ScreenshotUxmlTool().Invoke(new JObject());
            StringAssert.Contains("required", (string)res["error"]);
        }

        [Test]
        public void Uxml_NotFound_Error()
        {
            var res = new ScreenshotUxmlTool().Invoke(new JObject { ["uxml"] = "Assets/__NoSuchAsset__.uxml" });
            StringAssert.Contains("not found", (string)res["error"]);
        }
    }
}
