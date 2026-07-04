using NUnit.Framework;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;
using Shtl.Mcp.Tools;

namespace Shtl.Mcp.Editor.Tests
{
    /// T6b: get_object/modify_object (SerializedObject) + get/set_selection на реальной сцене. Чистка в TearDown.
    /// M5/AC3.11: bulk/nested/транзакционность + target по asset-path/instanceId.
    public class SceneEditToolsTests
    {
        const string Obj = "ShtlT6bObj";
        const string SoPath = "Assets/ShtlM5TestConfig.asset";
        Object[] _savedSelection;

        [SetUp]
        public void SetUp()
        {
            _savedSelection = Selection.objects;
        }

        [TearDown]
        public void TearDown()
        {
            Selection.objects = _savedSelection;
            var go = GameObject.Find(Obj);
            while (go != null)
            {
                Object.DestroyImmediate(go);
                go = GameObject.Find(Obj);
            }
            AssetDatabase.DeleteAsset(SoPath);
        }

        [Test]
        public void ModifyObject_BoolProperty_AndGetReflectsIt()
        {
            new GameObjectCreateTool().Invoke(new JObject { ["name"] = Obj, ["primitive"] = "cube" });

            var mod = new ModifyObjectTool().Invoke(new JObject
            {
                ["target"] = Obj,
                ["component"] = "BoxCollider",
                ["property"] = "m_IsTrigger",
                ["value"] = true
            });
            Assert.IsTrue((bool)mod["modified"], "modify_object");
            Assert.IsTrue(GameObject.Find(Obj).GetComponent<BoxCollider>().isTrigger, "isTrigger применён");

            var got = new GetObjectTool().Invoke(new JObject { ["target"] = Obj });
            StringAssert.Contains("BoxCollider", got["components"].ToString());
            StringAssert.Contains("m_IsTrigger", got["components"].ToString());
        }

        [Test]
        public void ModifyObject_WrongValueType_ReturnsStructuredError()
        {
            new GameObjectCreateTool().Invoke(new JObject { ["name"] = Obj, ["primitive"] = "cube" });
            // m_Size — Vector3; строка вместо [x,y,z] → структурный {error}, а не краш/исключение
            var res = new ModifyObjectTool().Invoke(new JObject
            {
                ["target"] = Obj,
                ["component"] = "BoxCollider",
                ["property"] = "m_Size",
                ["value"] = "notavector"
            });
            Assert.IsNotNull(res["error"], "неверный тип value → structured error");
        }

        [Test]
        public void ModifyObject_MissingComponent_Error()
        {
            new GameObjectCreateTool().Invoke(new JObject { ["name"] = Obj });
            var res = new ModifyObjectTool().Invoke(new JObject
            {
                ["target"] = Obj,
                ["component"] = "Rigidbody",
                ["property"] = "m_Mass",
                ["value"] = 5f
            });
            StringAssert.Contains("component not found", (string)res["error"]);
        }

        [Test]
        public void ModifyObject_Bulk_NestedPaths_OneTransaction()
        {
            new GameObjectCreateTool().Invoke(new JObject { ["name"] = Obj, ["primitive"] = "cube" });

            var res = new ModifyObjectTool().Invoke(new JObject
            {
                ["target"] = Obj,
                ["changes"] = new JArray
                {
                    new JObject { ["component"] = "BoxCollider", ["property"] = "m_IsTrigger", ["value"] = true },
                    new JObject { ["component"] = "BoxCollider", ["property"] = "m_Size.x", ["value"] = 3.5f }
                }
            });
            Assert.IsTrue((bool)res["modified"], res.ToString());
            Assert.AreEqual(2, (int)res["applied"]);

            var col = GameObject.Find(Obj).GetComponent<BoxCollider>();
            Assert.IsTrue(col.isTrigger, "bulk-изменение №1 применено");
            Assert.AreEqual(3.5f, col.size.x, 1e-4f, "вложенный путь m_Size.x применён");
        }

        [Test]
        public void ModifyObject_Bulk_BadChange_NothingApplied()
        {
            new GameObjectCreateTool().Invoke(new JObject { ["name"] = Obj, ["primitive"] = "cube" });

            var res = new ModifyObjectTool().Invoke(new JObject
            {
                ["target"] = Obj,
                ["changes"] = new JArray
                {
                    new JObject { ["component"] = "BoxCollider", ["property"] = "m_IsTrigger", ["value"] = true },
                    new JObject { ["component"] = "BoxCollider", ["property"] = "m_Bogus", ["value"] = 1 }
                }
            });
            StringAssert.Contains("property not found", (string)res["error"]);
            Assert.IsFalse(GameObject.Find(Obj).GetComponent<BoxCollider>().isTrigger,
                "транзакционность: валидное изменение из битого батча НЕ применено");
        }

        [Test]
        public void ModifyObject_AssetTarget_AndGetObject_ByPathAndInstanceId()
        {
            var so = ScriptableObject.CreateInstance<ShtlM5TestConfig>();
            AssetDatabase.CreateAsset(so, SoPath);

            var mod = new ModifyObjectTool().Invoke(new JObject
            {
                ["target"] = SoPath,
                ["changes"] = new JArray
                {
                    new JObject { ["property"] = "number", ["value"] = 42 },
                    new JObject { ["property"] = "vec.y", ["value"] = 1.5f }
                }
            });
            Assert.IsTrue((bool)mod["modified"], mod.ToString());

            var loaded = AssetDatabase.LoadAssetAtPath<ShtlM5TestConfig>(SoPath);
            Assert.AreEqual(42, loaded.number, "правка ассета по asset-path применена");
            Assert.AreEqual(1.5f, loaded.vec.y, 1e-4f, "вложенный путь на ассете применён");

            var byPath = new GetObjectTool().Invoke(new JObject { ["target"] = SoPath });
            Assert.AreEqual(42, (int)byPath["properties"]["number"], byPath.ToString());
            Assert.AreEqual(SoPath, (string)byPath["assetPath"]);

            var byId = new GetObjectTool().Invoke(new JObject { ["target"] = loaded.GetInstanceID() });
            Assert.AreEqual(42, (int)byId["properties"]["number"], "target по instanceId");
        }

        [Test]
        public void ModifyObject_ComponentOnAssetTarget_Error()
        {
            var so = ScriptableObject.CreateInstance<ShtlM5TestConfig>();
            AssetDatabase.CreateAsset(so, SoPath);

            var res = new ModifyObjectTool().Invoke(new JObject
            {
                ["target"] = SoPath,
                ["component"] = "BoxCollider",
                ["property"] = "number",
                ["value"] = 1
            });
            StringAssert.Contains("GameObject targets only", (string)res["error"]);
        }

        [Test]
        public void GetObject_NestedDepth_ExpandsStructs()
        {
            new GameObjectCreateTool().Invoke(new JObject { ["name"] = Obj, ["primitive"] = "cube" });
            var res = new GetObjectTool().Invoke(new JObject { ["target"] = Obj, ["maxDepth"] = 2 });
            Assert.IsNull(res["error"], res.ToString());
            StringAssert.Contains("BoxCollider", res["components"].ToString());
            StringAssert.Contains("m_Size", res["components"].ToString(), "сериализованные свойства достаточно глубоко (AC3.11в)");
        }

        [Test]
        public void Selection_SetThenGet()
        {
            new GameObjectCreateTool().Invoke(new JObject { ["name"] = Obj });

            var set = new SetSelectionTool().Invoke(new JObject { ["target"] = Obj });
            Assert.AreEqual(1, (int)set["selected"]);

            var get = new GetSelectionTool().Invoke(new JObject());
            StringAssert.Contains(Obj, get["selection"].ToString());
        }
    }
}
