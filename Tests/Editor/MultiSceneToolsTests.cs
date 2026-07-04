using NUnit.Framework;
using Newtonsoft.Json.Linq;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
using Shtl.Mcp.Tools;

namespace Shtl.Mcp.Editor.Tests
{
    /// M5/AC3.13: list/create/unload/set_active_scene — реальный аддитивный multi-scene setup.
    public class MultiSceneToolsTests
    {
        const string ScenePath = "Assets/ShtlM5Scene.unity";
        const string BasePath = "Assets/ShtlM5Base.unity";

        bool _savedBase;

        [TearDown]
        public void TearDown()
        {
            // закрыть пробную сцену, если тест упал посередине
            var probe = SceneManager.GetSceneByPath(ScenePath);
            if (probe.IsValid() && probe.isLoaded && SceneManager.sceneCount > 1)
            {
                EditorSceneManager.CloseScene(probe, true);
            }
            // активная сцена сохранялась в asset ради NewScene(Additive) → вернуть untitled-состояние
            if (_savedBase)
            {
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                _savedBase = false;
            }
            AssetDatabase.DeleteAsset(ScenePath);
            AssetDatabase.DeleteAsset(BasePath);
        }

        /// Unity запрещает вторую untitled-сцену → перед NewScene(Additive) сохранить активную untitled.
        void EnsureActiveSceneSaved()
        {
            var active = SceneManager.GetActiveScene();
            if (string.IsNullOrEmpty(active.path))
            {
                Assert.IsTrue(EditorSceneManager.SaveScene(active, BasePath), "прекондиция: активная сцена сохранена");
                _savedBase = true;
            }
        }

        [Test]
        public void ListScenes_ReportsExactlyOneActive()
        {
            var res = new ListScenesTool().Invoke(new JObject());
            Assert.GreaterOrEqual((int)res["count"], 1, res.ToString());
            var actives = ((JArray)res["scenes"]).Count(s => (bool)s["isActive"]);
            Assert.AreEqual(1, actives, "ровно одна активная сцена");
        }

        [Test]
        public void Create_SetActive_Unload_Lifecycle()
        {
            EnsureActiveSceneSaved();
            var baseScene = SceneManager.GetActiveScene();
            int before = SceneManager.sceneCount;

            var created = new CreateSceneTool().Invoke(new JObject { ["path"] = ScenePath });
            Assert.IsNull(created["error"], created.ToString());
            Assert.IsTrue((bool)created["created"], created.ToString());
            Assert.AreEqual(ScenePath, (string)created["path"], "сцена сохранена в asset");
            Assert.IsTrue((bool)created["isActive"], "NewScene(Additive) делает новую сцену активной");
            Assert.AreEqual(before + 1, SceneManager.sceneCount, "аддитивно: прежние сцены открыты");
            Assert.IsNotEmpty(AssetDatabase.AssetPathToGUID(ScenePath), "asset существует");

            // реальное переключение: probe → base (create уже активировал probe)
            var toBase = new SetActiveSceneTool().Invoke(new JObject { ["scene"] = baseScene.path });
            Assert.IsNull(toBase["error"], toBase.ToString());
            Assert.AreEqual(baseScene.path, SceneManager.GetActiveScene().path);

            // и обратно: base → probe
            var toProbe = new SetActiveSceneTool().Invoke(new JObject { ["scene"] = ScenePath });
            Assert.IsNull(toProbe["error"], toProbe.ToString());
            Assert.IsTrue((bool)toProbe["activated"], toProbe.ToString());
            Assert.AreEqual(ScenePath, SceneManager.GetActiveScene().path);

            // вернуть исходную активную (unload активной сам переключит, но явность надёжнее)
            SceneManager.SetActiveScene(baseScene);

            var unloaded = new UnloadSceneTool().Invoke(new JObject { ["scene"] = ScenePath });
            Assert.IsNull(unloaded["error"], unloaded.ToString());
            Assert.IsTrue((bool)unloaded["unloaded"], unloaded.ToString());
            Assert.AreEqual(before, SceneManager.sceneCount, "выгрузка вернула исходный набор");
        }

        [Test]
        public void Unload_UnknownScene_Error()
        {
            var res = new UnloadSceneTool().Invoke(new JObject { ["scene"] = "NoSuchScene" });
            StringAssert.Contains("no open scene", (string)res["error"]);
        }

        [Test]
        public void SetActive_UnknownScene_Error()
        {
            var res = new SetActiveSceneTool().Invoke(new JObject { ["scene"] = "NoSuchScene" });
            StringAssert.Contains("no open scene", (string)res["error"]);
        }
    }
}
