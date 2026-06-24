using NUnit.Framework;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;
using Shtl.Mcp.Tools;

namespace Shtl.Mcp.Editor.Tests
{
    /// T6b: get_object/modify_object (SerializedObject) + get/set_selection на реальной сцене. Чистка в TearDown.
    public class SceneEditToolsTests
    {
        const string Obj = "ShtlT6bObj";
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
