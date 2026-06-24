using NUnit.Framework;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Shtl.Mcp.Tools;

namespace Shtl.Mcp.Editor.Tests
{
    /// T5: prefab-цикл create→open→close→instantiate на реальном PrefabUtility/PrefabStage. Чистится в TearDown.
    public class PrefabToolsTests
    {
        // basename файла == имя корня: Unity именует корень пустого prefab по basename ассета.
        const string RootName = "ShtlMcpT5Root";
        const string PrefabPath = "Assets/ShtlMcpT5Root.prefab";

        [TearDown]
        public void TearDown()
        {
            if (PrefabStageUtility.GetCurrentPrefabStage() != null)
            {
                StageUtility.GoToMainStage();
            }
            var inst = GameObject.Find(RootName);
            if (inst != null)
            {
                Object.DestroyImmediate(inst);
            }
            if (AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath) != null)
            {
                AssetDatabase.DeleteAsset(PrefabPath);
            }
        }

        [Test]
        public void Prefab_RoundTrip()
        {
            // create (пустой)
            var created = new CreatePrefabTool().Invoke(new JObject { ["path"] = PrefabPath, ["name"] = RootName });
            Assert.IsTrue((bool)created["created"], "create_prefab");
            Assert.IsNotNull(AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath), "ассет появился");

            // open prefab-stage
            var opened = new OpenPrefabTool().Invoke(new JObject { ["path"] = PrefabPath });
            Assert.IsTrue((bool)opened["opened"], "open_prefab");
            Assert.AreEqual(RootName, (string)opened["root"]);
            Assert.IsNotNull(PrefabStageUtility.GetCurrentPrefabStage(), "stage открыт");

            // save (без правок — просто проверяем контракт)
            var saved = new SavePrefabTool().Invoke(new JObject());
            Assert.IsTrue((bool)saved["saved"], "save_prefab");

            // close
            var closed = new ClosePrefabTool().Invoke(new JObject());
            Assert.IsTrue((bool)closed["closed"], "close_prefab");
            Assert.IsNull(PrefabStageUtility.GetCurrentPrefabStage(), "stage закрыт");

            // instantiate в активную сцену
            var inst = new InstantiatePrefabTool().Invoke(new JObject { ["path"] = PrefabPath });
            Assert.IsTrue((bool)inst["instantiated"], "instantiate_prefab");
            Assert.IsNotNull(GameObject.Find(RootName), "инстанс в сцене");
        }

        [Test]
        public void SavePrefab_NoStage_Error()
        {
            if (PrefabStageUtility.GetCurrentPrefabStage() != null)
            {
                StageUtility.GoToMainStage();
            }
            var res = new SavePrefabTool().Invoke(new JObject());
            StringAssert.Contains("no prefab stage", (string)res["error"]);
        }

        [Test]
        public void InstantiatePrefab_MissingPath_Error()
        {
            var res = new InstantiatePrefabTool().Invoke(new JObject { ["path"] = "Assets/__nope__.prefab" });
            StringAssert.Contains("no prefab", (string)res["error"]);
        }
    }
}
