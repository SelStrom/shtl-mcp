using System;
using NUnit.Framework;
using Newtonsoft.Json.Linq;
using UnityEngine;
using Shtl.Mcp.Tools;

namespace Shtl.Mcp.Editor.Tests
{
    /// Мишень для call_method/find_method: static/private/перегрузки/исключение.
    public static class ShtlM5ReflectProbe
    {
        public static int Add(int a, int b) => a + b;
        static string Secret() => "shh";
        public static int Pick(int x) => 1;
        public static int Pick(string s) => 2;
        public static void Boom() => throw new InvalidOperationException("kaboom");
    }

    /// Мишень instance-вызова через instanceId (ScriptableObject имеет instanceId без сцены).
    public class ShtlM5ReflectSo : ScriptableObject
    {
        public int seed;
        public int GetSeed() => seed;
    }

    /// M5/AC3.12: call_method/find_method — реальная рефлексия по загруженным сборкам.
    public class ReflectionToolsTests
    {
        const string Probe = "Shtl.Mcp.Editor.Tests.ShtlM5ReflectProbe";
        const string Obj = "ShtlM5ReflectObj";

        [TearDown]
        public void TearDown()
        {
            var go = GameObject.Find(Obj);
            if (go != null)
            {
                UnityEngine.Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void CallStatic_Add()
        {
            var res = new CallMethodTool().Invoke(new JObject
            {
                ["type"] = Probe,
                ["method"] = "Add",
                ["args"] = new JArray { 2, 3 }
            });
            Assert.IsTrue((bool)res["ok"], res.ToString());
            Assert.AreEqual(5, (int)res["result"]);
        }

        [Test]
        public void CallPrivateStatic_Secret()
        {
            var res = new CallMethodTool().Invoke(new JObject { ["type"] = Probe, ["method"] = "Secret" });
            Assert.IsTrue((bool)res["ok"], res.ToString());
            Assert.AreEqual("shh", (string)res["result"]);
        }

        [Test]
        public void Overload_WithoutParameterTypes_AmbiguousError()
        {
            var res = new CallMethodTool().Invoke(new JObject
            {
                ["type"] = Probe,
                ["method"] = "Pick",
                ["args"] = new JArray { "hi" }
            });
            StringAssert.Contains("ambiguous", (string)res["error"]);
            Assert.IsNotNull(res["overloads"], "перечислены сигнатуры для выбора");
        }

        [Test]
        public void Overload_ParameterTypes_PicksRight()
        {
            var res = new CallMethodTool().Invoke(new JObject
            {
                ["type"] = Probe,
                ["method"] = "Pick",
                ["parameterTypes"] = new JArray { "String" },
                ["args"] = new JArray { "hi" }
            });
            Assert.IsTrue((bool)res["ok"], res.ToString());
            Assert.AreEqual(2, (int)res["result"], "перегрузка со string");
        }

        [Test]
        public void MethodThrows_StructuredError()
        {
            var res = new CallMethodTool().Invoke(new JObject { ["type"] = Probe, ["method"] = "Boom" });
            StringAssert.Contains("InvalidOperationException", (string)res["error"]);
            StringAssert.Contains("kaboom", (string)res["error"]);
        }

        [Test]
        public void ArgsCountMismatch_Error()
        {
            var res = new CallMethodTool().Invoke(new JObject
            {
                ["type"] = Probe,
                ["method"] = "Add",
                ["parameterTypes"] = new JArray { "Int32", "Int32" },
                ["args"] = new JArray { 1 }
            });
            StringAssert.Contains("args count mismatch", (string)res["error"]);
        }

        [Test]
        public void Instance_ViaInstanceId()
        {
            var so = ScriptableObject.CreateInstance<ShtlM5ReflectSo>();
            try
            {
                so.seed = 7;
                var res = new CallMethodTool().Invoke(new JObject
                {
                    ["type"] = "ShtlM5ReflectSo",
                    ["method"] = "GetSeed",
                    ["target"] = so.GetInstanceID()
                });
                Assert.IsTrue((bool)res["ok"], res.ToString());
                Assert.AreEqual(7, (int)res["result"]);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(so);
            }
        }

        [Test]
        public void Instance_ComponentViaGameObjectTarget()
        {
            var go = new GameObject(Obj);
            go.AddComponent<BoxCollider>();
            var res = new CallMethodTool().Invoke(new JObject
            {
                ["type"] = "UnityEngine.BoxCollider",
                ["method"] = "get_isTrigger",
                ["target"] = Obj
            });
            Assert.IsTrue((bool)res["ok"], res.ToString());
            Assert.AreEqual(false, (bool)res["result"], "GO-target резолвится в компонент");
        }

        [Test]
        public void Instance_MissingTarget_Error()
        {
            var res = new CallMethodTool().Invoke(new JObject { ["type"] = "ShtlM5ReflectSo", ["method"] = "GetSeed" });
            StringAssert.Contains("target", (string)res["error"]);
        }

        [Test]
        public void MethodNotFound_MentionsFindMethod()
        {
            var res = new CallMethodTool().Invoke(new JObject { ["type"] = Probe, ["method"] = "Nope" });
            StringAssert.Contains("find_method", (string)res["error"]);
        }

        [Test]
        public void TypeNotFound_Suggestions()
        {
            var res = new CallMethodTool().Invoke(new JObject { ["type"] = "ShtlM5ReflectPro", ["method"] = "Add" });
            StringAssert.Contains("type not found", (string)res["error"]);
            StringAssert.Contains("ShtlM5ReflectProbe", res["suggestions"].ToString());
        }

        [Test]
        public void FindMethod_ListsSignatures()
        {
            var res = new FindMethodTool().Invoke(new JObject { ["type"] = Probe, ["nameContains"] = "Pick" });
            Assert.AreEqual(2, (int)res["count"], res.ToString());
            StringAssert.Contains("Pick", res["methods"].ToString());
            StringAssert.Contains("parameterTypes", res["methods"][0].ToString(), "сигнатура пригодна для call_method");
        }
    }
}
