using NUnit.Framework;
using Newtonsoft.Json.Linq;
using UnityEngine;
using Shtl.Mcp.Tools;

namespace Shtl.Mcp.Editor.Tests
{
    /// M5/AC3.10: add_component/remove_component на реальных GameObject активной сцены.
    public class ComponentToolsTests
    {
        const string Obj = "ShtlM5CompObj";

        [TearDown]
        public void TearDown()
        {
            var go = GameObject.Find(Obj);
            while (go != null)
            {
                Object.DestroyImmediate(go);
                go = GameObject.Find(Obj);
            }
        }

        static GameObject NewObj()
        {
            var go = new GameObject(Obj);
            return go;
        }

        [Test]
        public void Add_ShortName_AndReadback()
        {
            var go = NewObj();
            var res = new AddComponentTool().Invoke(new JObject { ["target"] = Obj, ["type"] = "BoxCollider" });
            Assert.IsTrue((bool)res["added"], res.ToString());
            Assert.AreEqual("UnityEngine.BoxCollider", (string)res["type"]);
            Assert.IsNotNull(res["instanceId"], "ref для последующего modify_object");
            Assert.IsNotNull(go.GetComponent<BoxCollider>());
        }

        [Test]
        public void Add_FullName()
        {
            var go = NewObj();
            var res = new AddComponentTool().Invoke(new JObject { ["target"] = Obj, ["type"] = "UnityEngine.Rigidbody2D" });
            Assert.IsTrue((bool)res["added"], res.ToString());
            Assert.IsNotNull(go.GetComponent<Rigidbody2D>());
        }

        [Test]
        public void Add_UnknownType_ErrorWithSuggestions()
        {
            NewObj();
            var res = new AddComponentTool().Invoke(new JObject { ["target"] = Obj, ["type"] = "BoxColl" });
            StringAssert.Contains("type not found", (string)res["error"]);
            StringAssert.Contains("BoxCollider", res["suggestions"].ToString(), "подсказка по частичному совпадению");
        }

        [Test]
        public void Add_AbstractType_Error()
        {
            NewObj();
            var res = new AddComponentTool().Invoke(new JObject { ["target"] = Obj, ["type"] = "ShtlM5AbstractComp" });
            StringAssert.Contains("abstract", (string)res["error"]);
        }

        [Test]
        public void Add_DisallowMultiple_SecondTime_Error()
        {
            // ShtlM5SingleComp — runtime-фикстура в TestProject~/Assets (тип резолвится по имени)
            NewObj();
            var first = new AddComponentTool().Invoke(new JObject { ["target"] = Obj, ["type"] = "ShtlM5SingleComp" });
            Assert.IsNull(first["error"], first.ToString());
            Assert.IsTrue((bool)first["added"], first.ToString());
            var second = new AddComponentTool().Invoke(new JObject { ["target"] = Obj, ["type"] = "ShtlM5SingleComp" });
            StringAssert.Contains("DisallowMultiple", (string)second["error"]);
        }

        [Test]
        public void Remove_Works()
        {
            var go = NewObj();
            go.AddComponent<BoxCollider>();
            var res = new RemoveComponentTool().Invoke(new JObject { ["target"] = Obj, ["type"] = "BoxCollider" });
            Assert.IsTrue((bool)res["removed"], res.ToString());
            Assert.IsNull(go.GetComponent<BoxCollider>());
        }

        [Test]
        public void Remove_Transform_Error()
        {
            NewObj();
            var res = new RemoveComponentTool().Invoke(new JObject { ["target"] = Obj, ["type"] = "Transform" });
            StringAssert.Contains("Transform", (string)res["error"]);
        }

        [Test]
        public void Remove_Missing_Error()
        {
            NewObj();
            var res = new RemoveComponentTool().Invoke(new JObject { ["target"] = Obj, ["type"] = "BoxCollider" });
            StringAssert.Contains("component not found", (string)res["error"]);
        }

        [Test]
        public void Remove_RequiredComponent_Error()
        {
            var go = NewObj();
            go.AddComponent<HingeJoint>(); // [RequireComponent(typeof(Rigidbody))] — Rigidbody добавится сам
            Assert.IsNotNull(go.GetComponent<Rigidbody>(), "прекондиция: HingeJoint притащил Rigidbody");
            var res = new RemoveComponentTool().Invoke(new JObject { ["target"] = Obj, ["type"] = "Rigidbody" });
            StringAssert.Contains("HingeJoint", (string)res["error"], "внятная ошибка: кто требует");
            Assert.IsNotNull(go.GetComponent<Rigidbody>(), "required-компонент не удалён");
        }

        [Test]
        public void Remove_MultipleSameType_NeedsIndex()
        {
            var go = NewObj();
            go.AddComponent<BoxCollider>();
            go.AddComponent<BoxCollider>();

            var noIndex = new RemoveComponentTool().Invoke(new JObject { ["target"] = Obj, ["type"] = "BoxCollider" });
            StringAssert.Contains("index", (string)noIndex["error"]);
            Assert.AreEqual(2, go.GetComponents<BoxCollider>().Length, "без index ничего не удалено");

            var withIndex = new RemoveComponentTool().Invoke(new JObject { ["target"] = Obj, ["type"] = "BoxCollider", ["index"] = 1 });
            Assert.IsTrue((bool)withIndex["removed"], withIndex.ToString());
            Assert.AreEqual(1, go.GetComponents<BoxCollider>().Length);
        }
    }
}
