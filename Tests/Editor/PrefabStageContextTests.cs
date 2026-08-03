using NUnit.Framework;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Shtl.Mcp.Tools;

namespace Shtl.Mcp.Editor.Tests
{
    /// AC3.16: пока открыт prefab-stage, объектные тулы работают по нему, а не по активной сцене.
    /// Плюс `parent` у instantiate_prefab. Всё на реальном PrefabStage, чистится в TearDown.
    public class PrefabStageContextTests
    {
        const string RootName = "ShtlStageRoot";
        const string PrefabPath = "Assets/ShtlStageRoot.prefab";
        const string ChildName = "ShtlStageChild";
        const string SceneOnlyName = "ShtlStageSceneOnly";

        [SetUp]
        public void SetUp()
        {
            GoToMainStage();
            // Одноимённый объект в сцене — ловушка на то, что резолв не свалится обратно в сцену.
            var sceneOnly = new GameObject(SceneOnlyName);
            sceneOnly.transform.SetParent(null);
            new CreatePrefabTool().Invoke(new JObject { ["path"] = PrefabPath, ["name"] = RootName });
        }

        [TearDown]
        public void TearDown()
        {
            GoToMainStage();
            foreach (var n in new[] { RootName, ChildName, SceneOnlyName })
            {
                var go = GameObject.Find(n);
                while (go != null)
                {
                    Object.DestroyImmediate(go);
                    go = GameObject.Find(n);
                }
            }
            if (AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath) != null)
            {
                AssetDatabase.DeleteAsset(PrefabPath);
            }
        }

        /// Через тул, а не через StageUtility напрямую: грязный стейдж иначе поднимает блокирующий
        /// "Prefab Has Been Modified" — интерактивно это висяк главного потока, в batch — отменённый
        /// диалог, оставленный открытым стейдж и падение всех последующих тестов.
        static void GoToMainStage()
        {
            new ClosePrefabTool().Invoke(new JObject { ["policy"] = "discard" });
        }

        [Test]
        public void Context_IsScene_WhenNoStageOpen()
        {
            var hier = new GetHierarchyTool().Invoke(new JObject());
            Assert.AreEqual("scene", (string)hier["context"]["kind"]);

            var found = new FindGameObjectTool().Invoke(new JObject { ["name"] = SceneOnlyName });
            Assert.AreEqual(1, (int)found["count"], "объект сцены виден без стейджа");
            Assert.AreEqual("scene", (string)found["context"]["kind"]);
        }

        [Test]
        public void OpenStage_SwitchesResolutionToStage()
        {
            new OpenPrefabTool().Invoke(new JObject { ["path"] = PrefabPath });

            var hier = new GetHierarchyTool().Invoke(new JObject());
            Assert.AreEqual("prefabStage", (string)hier["context"]["kind"], "контекст = стейдж");
            Assert.AreEqual(PrefabPath, (string)hier["context"]["prefabPath"]);
            StringAssert.Contains(RootName, hier["tree"].ToString(), "дерево — содержимое префаба");

            // корень префаба резолвится, а объект активной сцены — уже нет
            var found = new FindGameObjectTool().Invoke(new JObject { ["name"] = RootName });
            Assert.AreEqual(1, (int)found["count"], "корень префаба найден в стейдже");

            var sceneOnly = new FindGameObjectTool().Invoke(new JObject { ["name"] = SceneOnlyName });
            Assert.AreEqual(0, (int)sceneOnly["count"], "объекты сцены из стейджа не видны");
            Assert.AreEqual("prefabStage", (string)sceneOnly["context"]["kind"], "контекст объясняет пустой результат");
        }

        [Test]
        public void EditingInsideStage_TargetsStageScene()
        {
            new OpenPrefabTool().Invoke(new JObject { ["path"] = PrefabPath });

            var created = new GameObjectCreateTool().Invoke(new JObject { ["name"] = ChildName, ["parent"] = RootName });
            Assert.AreEqual(RootName + "/" + ChildName, (string)created["path"], "объект создан внутри префаба");

            var stage = PrefabStageUtility.GetCurrentPrefabStage();
            var child = stage.prefabContentsRoot.transform.Find(ChildName);
            Assert.IsNotNull(child, "объект действительно в сцене стейджа");
            Assert.AreEqual(stage.scene, child.gameObject.scene, "объект принадлежит сцене стейджа, а не активной");
        }

        [Test]
        public void ClosingStage_ReturnsResolutionToScene()
        {
            new OpenPrefabTool().Invoke(new JObject { ["path"] = PrefabPath });
            Assert.AreEqual("prefabStage", (string)new GetHierarchyTool().Invoke(new JObject())["context"]["kind"]);

            GoToMainStage();

            var hier = new GetHierarchyTool().Invoke(new JObject());
            Assert.AreEqual("scene", (string)hier["context"]["kind"], "после закрытия — снова сцена");
            var found = new FindGameObjectTool().Invoke(new JObject { ["name"] = SceneOnlyName });
            Assert.AreEqual(1, (int)found["count"], "объект сцены снова виден");
        }

        [Test]
        public void InstantiatePrefab_Parent_ParentsAndKeepsLocalTransform()
        {
            var host = new GameObject(ChildName);
            host.transform.position = new Vector3(10f, 20f, 30f);

            var res = new InstantiatePrefabTool().Invoke(new JObject { ["path"] = PrefabPath, ["parent"] = ChildName });
            Assert.IsTrue((bool)res["instantiated"], "instantiate_prefab");
            Assert.AreEqual(ChildName + "/" + RootName, (string)res["path"], "путь инстанса под родителем");

            var inst = host.transform.Find(RootName);
            Assert.IsNotNull(inst, "инстанс под указанным родителем");
            Assert.AreEqual(Vector3.zero, inst.localPosition, "сохранён авторский локальный трансформ, а не мировой");
        }

        [Test]
        public void InstantiatePrefab_UnknownParent_ErrorsWithoutLeavingGarbage()
        {
            var res = new InstantiatePrefabTool().Invoke(new JObject { ["path"] = PrefabPath, ["parent"] = "__nope__" });
            StringAssert.Contains("parent not found", (string)res["error"]);
            Assert.IsNull(GameObject.Find(RootName), "инстанс не создавался");
        }

        [Test]
        public void ClosePrefab_DirtyStage_DiscardsWithoutModal()
        {
            new OpenPrefabTool().Invoke(new JObject { ["path"] = PrefabPath });
            new GameObjectCreateTool().Invoke(new JObject { ["name"] = ChildName, ["parent"] = RootName });
            Assert.IsTrue(PrefabStageUtility.GetCurrentPrefabStage().scene.isDirty, "стейдж грязный");

            var res = new ClosePrefabTool().Invoke(new JObject());
            Assert.IsTrue((bool)res["closed"], "закрылся");
            Assert.IsTrue((bool)res["wasDirty"]);
            Assert.IsFalse((bool)res["saved"], "discard не сохраняет");
            Assert.IsNull(PrefabStageUtility.GetCurrentPrefabStage(), "стейдж действительно закрыт");

            var reopened = PrefabUtility.LoadPrefabContents(PrefabPath);
            try
            {
                Assert.IsNull(reopened.transform.Find(ChildName), "правка отброшена, в ассет не попала");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(reopened);
            }
        }

        [Test]
        public void ClosePrefab_DirtyStage_SavePolicyWritesAsset()
        {
            new OpenPrefabTool().Invoke(new JObject { ["path"] = PrefabPath });
            new GameObjectCreateTool().Invoke(new JObject { ["name"] = ChildName, ["parent"] = RootName });

            var res = new ClosePrefabTool().Invoke(new JObject { ["policy"] = "save" });
            Assert.IsTrue((bool)res["closed"]);
            Assert.IsTrue((bool)res["saved"], "policy=save сохранил");

            var reopened = PrefabUtility.LoadPrefabContents(PrefabPath);
            try
            {
                Assert.IsNotNull(reopened.transform.Find(ChildName), "правка доехала до ассета");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(reopened);
            }
        }

        [Test]
        public void ClosePrefab_DirtyStage_AbortKeepsStageOpen()
        {
            new OpenPrefabTool().Invoke(new JObject { ["path"] = PrefabPath });
            new GameObjectCreateTool().Invoke(new JObject { ["name"] = ChildName, ["parent"] = RootName });

            var res = new ClosePrefabTool().Invoke(new JObject { ["policy"] = "abort" });
            StringAssert.Contains("unsaved changes", (string)res["error"]);
            Assert.IsNotNull(PrefabStageUtility.GetCurrentPrefabStage(), "стейдж остался открыт");
        }

        [Test]
        public void InstantiatePrefab_InsideStage_GoesIntoStageScene()
        {
            // отдельный префаб, чтобы не вкладывать префаб сам в себя
            const string hostPath = "Assets/ShtlStageHost.prefab";
            try
            {
                new CreatePrefabTool().Invoke(new JObject { ["path"] = hostPath, ["name"] = "ShtlStageHost" });
                new OpenPrefabTool().Invoke(new JObject { ["path"] = hostPath });

                var res = new InstantiatePrefabTool().Invoke(new JObject { ["path"] = PrefabPath });
                Assert.IsTrue((bool)res["instantiated"], "instantiate_prefab в стейдже");
                Assert.AreEqual("prefabStage", (string)res["context"]["kind"]);

                // GameObject.Find не ищет в preview-сцене стейджа — идём по корням самой сцены стейджа.
                var stage = PrefabStageUtility.GetCurrentPrefabStage();
                GameObject inst = null;
                foreach (var go in stage.scene.GetRootGameObjects())
                {
                    if (go.name == RootName)
                    {
                        inst = go;
                    }
                }
                Assert.IsNotNull(inst, "инстанс лежит в сцене стейджа");
                Assert.IsNull(GameObject.Find(RootName), "и не в активной сцене");
            }
            finally
            {
                GoToMainStage();
                if (AssetDatabase.LoadAssetAtPath<GameObject>(hostPath) != null)
                {
                    AssetDatabase.DeleteAsset(hostPath);
                }
            }
        }
    }
}
