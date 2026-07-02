using System;
using System.Collections.Generic;
using NUnit.Framework;
using Newtonsoft.Json.Linq;
using Shtl.Mcp.Tools;

namespace Shtl.Mcp.Editor.Tests
{
    /// F3/AC3.6–3.8: дискавери кастомных тулов. Тестируем ядро RegisterFrom по ЯВНЫМ спискам типов — тест-тулы
    /// НЕ помечены [McpTool], поэтому живой сервер (TypeCache.GetTypesWithAttribute) их не подхватывает.
    public class ToolDiscoveryTests
    {
        List<string> _skips;
        Action<Type, string> _collect;

        [SetUp]
        public void SetUp()
        {
            _skips = new List<string>();
            _collect = (t, why) => _skips.Add((t != null ? t.Name : "?") + ": " + why);
        }

        [Test] public void RegistersValidTool()
        {
            var r = new ToolRegistry();
            ToolDiscovery.RegisterFrom(new[] { typeof(GoodTool) }, r, _collect);
            Assert.IsTrue(r.Contains("shtl_test_good"));
            Assert.IsEmpty(_skips);
        }

        // RED-gate изоляции (INV-5): битый тул НЕ должен мешать регистрации соседа.
        [Test] public void SkipsThrowingCtor_ButRegistersSibling()
        {
            var r = new ToolRegistry();
            ToolDiscovery.RegisterFrom(new[] { typeof(ThrowTool), typeof(GoodTool) }, r, _collect);
            Assert.IsFalse(r.Contains("shtl_test_throw"), "битый тул пропущен");
            Assert.IsTrue(r.Contains("shtl_test_good"), "сосед зарегистрирован несмотря на битый");
            Assert.AreEqual(1, _skips.Count);
        }

        [Test] public void SkipsNoParameterlessCtor()
        {
            var r = new ToolRegistry();
            ToolDiscovery.RegisterFrom(new[] { typeof(NoParamTool) }, r, _collect);
            Assert.IsFalse(r.Contains("shtl_test_noparam"));
            Assert.AreEqual(1, _skips.Count);
        }

        [Test] public void SkipsEmptyName()
        {
            var r = new ToolRegistry();
            ToolDiscovery.RegisterFrom(new[] { typeof(EmptyNameTool) }, r, _collect);
            Assert.AreEqual(1, _skips.Count);
        }

        [Test] public void SkipsNonITool()
        {
            var r = new ToolRegistry();
            ToolDiscovery.RegisterFrom(new[] { typeof(NotATool) }, r, _collect);
            Assert.AreEqual(1, _skips.Count);
        }

        // Встроенные приоритетны: имя, совпавшее с уже зарегистрированным, — пропуск без override.
        [Test] public void BuiltinWins_OnNameCollision()
        {
            var r = new ToolRegistry();
            var builtin = new StatusNameTool();
            r.Register(builtin); // имитируем встроенный, зарегистрированный первым
            ToolDiscovery.RegisterFrom(new[] { typeof(StatusNameTool) }, r, _collect);
            Assert.AreSame(builtin, r.Get("status"), "встроенный не перетёрт кастомным");
            Assert.AreEqual(1, _skips.Count);
        }

        [Test] public void CustomVsCustom_FirstWins()
        {
            var r = new ToolRegistry();
            ToolDiscovery.RegisterFrom(new[] { typeof(GoodTool), typeof(DupTool) }, r, _collect);
            Assert.IsInstanceOf<GoodTool>(r.Get("shtl_test_good"), "первый найденный побеждает");
            Assert.AreEqual(1, _skips.Count, "второй с тем же именем пропущен");
        }

        // Геттер Name бросает → изоляция должна пропустить тул, а не пробить наружу (AC3.7).
        [Test] public void SkipsThrowingName_ButRegistersSibling()
        {
            var r = new ToolRegistry();
            ToolDiscovery.RegisterFrom(new[] { typeof(NameThrowsTool), typeof(GoodTool) }, r, _collect);
            Assert.IsTrue(r.Contains("shtl_test_good"), "сосед зарегистрирован несмотря на битый Name-геттер");
            Assert.AreEqual(1, _skips.Count);
        }

        // Геттер InputSchema бросает → прогрев контракта на обнаружении пропускает тул (иначе отравил бы tools/list).
        [Test] public void SkipsThrowingInputSchema_ButRegistersSibling()
        {
            var r = new ToolRegistry();
            ToolDiscovery.RegisterFrom(new[] { typeof(SchemaThrowsTool), typeof(GoodTool) }, r, _collect);
            Assert.IsFalse(r.Contains("shtl_test_schema_throws"), "тул с битой схемой не зарегистрирован");
            Assert.IsTrue(r.Contains("shtl_test_good"));
            Assert.AreEqual(1, _skips.Count);
        }

        [Test] public void SkipsAbstractTool()
        {
            var r = new ToolRegistry();
            ToolDiscovery.RegisterFrom(new[] { typeof(TestToolBase), typeof(GoodTool) }, r, _collect);
            Assert.IsTrue(r.Contains("shtl_test_good"));
            Assert.AreEqual(1, _skips.Count, "абстрактный кандидат пропущен");
        }

        // ── тест-тулы (НЕ помечены [McpTool] → не видны живому серверу) ──────

        abstract class TestToolBase : ITool
        {
            public abstract string Name { get; }
            public string Description => "test tool";
            public JObject InputSchema => new JObject { ["type"] = "object" };
            public bool NeedsMainThread => false;
            public JObject Invoke(JObject args) => new JObject { ["ok"] = true };
        }

        sealed class GoodTool : TestToolBase { public override string Name => "shtl_test_good"; }
        sealed class DupTool : TestToolBase { public override string Name => "shtl_test_good"; }
        sealed class EmptyNameTool : TestToolBase { public override string Name => ""; }
        sealed class StatusNameTool : TestToolBase { public override string Name => "status"; }

        sealed class ThrowTool : TestToolBase
        {
            public ThrowTool() { throw new Exception("boom in ctor"); }
            public override string Name => "shtl_test_throw";
        }

        sealed class NoParamTool : TestToolBase
        {
            public NoParamTool(int _) { } // нет public parameterless-ctor
            public override string Name => "shtl_test_noparam";
        }

        sealed class NameThrowsTool : TestToolBase
        {
            public override string Name => throw new Exception("name getter boom");
        }

        // Валидный Name/ctor, но битый InputSchema — прогрев контракта на обнаружении обязан его отсечь.
        sealed class SchemaThrowsTool : ITool
        {
            public string Name => "shtl_test_schema_throws";
            public string Description => "x";
            public bool NeedsMainThread => false;
            public JObject InputSchema => throw new Exception("schema getter boom");
            public JObject Invoke(JObject args) => new JObject { ["ok"] = true };
        }

        sealed class NotATool { } // не реализует ITool
    }
}
