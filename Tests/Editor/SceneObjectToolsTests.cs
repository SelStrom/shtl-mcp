using NUnit.Framework;
using Newtonsoft.Json.Linq;
using UnityEngine;
using Shtl.Mcp.Tools;

namespace Shtl.Mcp.Editor.Tests
{
    /// T6a: create/modify/destroy/set_parent/find/hierarchy на РЕАЛЬНОЙ активной сцене. Чистится в TearDown.
    public class SceneObjectToolsTests
    {
        const string Parent = "ShtlT6Parent";
        const string Child = "ShtlT6Child";

        [TearDown]
        public void TearDown()
        {
            foreach (var n in new[] { Parent, Child })
            {
                var go = GameObject.Find(n);
                while (go != null)
                {
                    Object.DestroyImmediate(go);
                    go = GameObject.Find(n);
                }
            }
        }

        [Test]
        public void GameObject_Create_Modify_Find_Reparent_Destroy()
        {
            // create parent + child (child сразу под parent)
            new GameObjectCreateTool().Invoke(new JObject { ["name"] = Parent });
            var childRes = new GameObjectCreateTool().Invoke(new JObject { ["name"] = Child, ["parent"] = Parent });
            Assert.AreEqual(Parent + "/" + Child, (string)childRes["path"], "child под parent");

            // modify: позиция child
            new GameObjectModifyTool().Invoke(new JObject
            {
                ["target"] = Parent + "/" + Child,
                ["position"] = new JArray { 1f, 2f, 3f }
            });
            Assert.AreEqual(new Vector3(1, 2, 3), GameObject.Find(Child).transform.localPosition);

            // find_gameobject
            var found = new FindGameObjectTool().Invoke(new JObject { ["name"] = Child });
            Assert.AreEqual(1, (int)found["count"]);

            // hierarchy от parent содержит child
            var hier = new GetHierarchyTool().Invoke(new JObject { ["root"] = Parent });
            StringAssert.Contains(Child, hier["tree"].ToString());

            // set_parent → child в корень (transform.parent == null)
            new SetParentTool().Invoke(new JObject { ["child"] = Parent + "/" + Child, ["parent"] = "" });
            Assert.IsNull(GameObject.Find(Child).transform.parent, "child теперь корневой");

            // destroy parent
            var del = new GameObjectDestroyTool().Invoke(new JObject { ["target"] = Parent });
            Assert.IsTrue((bool)del["destroyed"]);
            Assert.IsNull(GameObject.Find(Parent));
        }

        [Test]
        public void Modify_MissingTarget_Error()
        {
            var res = new GameObjectModifyTool().Invoke(new JObject { ["target"] = "__nope__" });
            StringAssert.Contains("not found", (string)res["error"]);
        }

        [Test]
        public void Create_Primitive_HasMeshFilter()
        {
            var res = new GameObjectCreateTool().Invoke(new JObject { ["name"] = Parent, ["primitive"] = "cube" });
            Assert.AreEqual(Parent, (string)res["name"]);
            Assert.IsNotNull(GameObject.Find(Parent).GetComponent<MeshFilter>(), "примитив cube → MeshFilter");
        }
    }
}
