using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Newtonsoft.Json.Linq;
using Shtl.Mcp.Tools;

namespace Shtl.Mcp.Editor.Tests
{
    /// T8: гейт run_csharp (footgun off → error), execute_menu_item, реальная компиляция run_csharp.
    public class EscapeHatchToolsTests
    {
        [Test]
        public void RunCsharp_Disabled_ReturnsGateError()
        {
            var res = new RunCsharpTool(() => false).Invoke(new JObject { ["code"] = "return 1;" });
            StringAssert.Contains("disabled", (string)res["error"]);
        }

        [Test]
        public void ExecuteMenuItem_Unknown_ReturnsFalse()
        {
            LogAssert.Expect(LogType.Error, new Regex("no menu named")); // Unity логирует error на неизвестном меню
            var res = new ExecuteMenuItemTool().Invoke(new JObject { ["menuItem"] = "Bogus/DoesNotExist" });
            Assert.IsFalse((bool)res["executed"]);
        }

        [Test]
        public void RunCsharp_Enabled_EvaluatesExpression()
        {
            var res = new RunCsharpTool(() => true).Invoke(new JObject { ["code"] = "return 40 + 2;" });
            Assert.IsNull(res["error"], "ожидали успешную компиляцию (CodeDom доступен)");
            Assert.IsTrue((bool)res["ok"]);
            Assert.AreEqual("42", (string)res["result"]);
        }
    }
}
