using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Shtl.Mcp.Tools;

namespace Shtl.Mcp.Editor.Tests
{
    /// Ветвление dirty-scene политики тест-прогона (AC4.9 modal-free): discard(дефолт)/save/abort,
    /// untitled и multi-scene → отказ. Чистый DecideScenePolicy — без Unity-сцены.
    public class RunTestsScenePolicyTests
    {
        [Test] public void NoDirtyScenes_Proceeds_NoAction()
        {
            var err = RunTestsTool.DecideScenePolicy(0, false, null, out bool save, out bool discard);
            Assert.IsNull(err);
            Assert.IsFalse(save);
            Assert.IsFalse(discard);
        }

        [Test] public void DefaultPolicy_IsDiscard()
        {
            var err = RunTestsTool.DecideScenePolicy(1, true, null, out bool save, out bool discard);
            Assert.IsNull(err);
            Assert.IsFalse(save);
            Assert.IsTrue(discard);
        }

        [Test] public void EmptyPolicy_IsDiscard()
        {
            var err = RunTestsTool.DecideScenePolicy(1, true, "", out _, out bool discard);
            Assert.IsNull(err);
            Assert.IsTrue(discard);
        }

        [Test] public void Save_OnSinglePathScene_ChoosesSave()
        {
            var err = RunTestsTool.DecideScenePolicy(1, true, "save", out bool save, out bool discard);
            Assert.IsNull(err);
            Assert.IsTrue(save);
            Assert.IsFalse(discard);
        }

        [Test] public void Policy_IsCaseInsensitive()
        {
            var err = RunTestsTool.DecideScenePolicy(1, true, "SAVE", out bool save, out _);
            Assert.IsNull(err);
            Assert.IsTrue(save);
        }

        [Test] public void Abort_ReturnsError_NoAction()
        {
            var err = RunTestsTool.DecideScenePolicy(1, true, "abort", out bool save, out bool discard);
            StringAssert.Contains("abort", err);
            Assert.IsFalse(save);
            Assert.IsFalse(discard);
        }

        [Test] public void UnknownPolicy_ReturnsError()
        {
            var err = RunTestsTool.DecideScenePolicy(1, true, "bogus", out _, out _);
            StringAssert.Contains("unknown scenePolicy", err);
        }

        [Test] public void MultipleDirtyScenes_Rejected()
        {
            var err = RunTestsTool.DecideScenePolicy(2, true, "save", out bool save, out bool discard);
            StringAssert.Contains("multiple", err);
            Assert.IsFalse(save);
            Assert.IsFalse(discard);
        }

        [Test] public void Untitled_Save_Rejected()
        {
            var err = RunTestsTool.DecideScenePolicy(1, false, "save", out _, out _);
            StringAssert.Contains("untitled", err);
        }

        [Test] public void Untitled_Discard_Rejected()
        {
            var err = RunTestsTool.DecideScenePolicy(1, false, "discard", out _, out _);
            StringAssert.Contains("untitled", err);
        }
    }

    /// Оболочка ResolveDirtyScenes на реальной сохранённой сцене — то, что run_tests делает до старта прогона
    /// любого режима.
    public class RunTestsResolveDirtySceneTests
    {
        const string ScenePath = "Assets/ShtlRunTestsDirtyScene.unity";
        const string ProbeName = "ShtlDirtyProbe";

        [TearDown]
        public void TearDown()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            AssetDatabase.DeleteAsset(ScenePath);
        }

        static Scene SavedSceneWithUnsavedProbe()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            Assert.IsTrue(EditorSceneManager.SaveScene(scene, ScenePath), "прекондиция: сцена сохранена");
            new GameObject(ProbeName);
            EditorSceneManager.MarkSceneDirty(scene);
            Assert.IsTrue(scene.isDirty, "прекондиция: сцена грязная");
            return scene;
        }

        [Test] public void DefaultPolicy_RevertsDirtySceneFromDisk()
        {
            SavedSceneWithUnsavedProbe();

            Assert.IsNull(RunTestsTool.ResolveDirtyScenes(null));

            Assert.IsFalse(SceneManager.GetSceneByPath(ScenePath).isDirty);
            Assert.IsNull(GameObject.Find(ProbeName), "несохранённый объект отброшен");
        }

        [Test] public void Abort_KeepsDirtySceneAndReturnsError()
        {
            var scene = SavedSceneWithUnsavedProbe();

            StringAssert.Contains("scenePolicy=abort", RunTestsTool.ResolveDirtyScenes("abort"));

            Assert.IsTrue(scene.isDirty);
            Assert.IsNotNull(GameObject.Find(ProbeName));
        }
    }
}
