using System.IO;
using System.Linq;
using NUnit.Framework;
using Newtonsoft.Json.Linq;
using UnityEditor;
using Shtl.Mcp.Logging;
using Shtl.Mcp.Tools;

namespace Shtl.Mcp.Editor.Tests
{
    /// T4: clear_logs + AssetDatabase CRUD (create_folder/find/read/move/delete) на РЕАЛЬНОМ AssetDatabase
    /// (real-over-mocks). Временная папка чистится в TearDown.
    public class AssetToolsTests
    {
        const string TmpFolder = "Assets/ShtlMcpT4Tmp";

        [TearDown]
        public void TearDown()
        {
            if (AssetDatabase.IsValidFolder(TmpFolder))
            {
                AssetDatabase.DeleteAsset(TmpFolder);
            }
        }

        [Test]
        public void ClearLogs_EmptiesBuffer()
        {
            var buf = new LogBuffer(50);
            buf.Add("a", "", LogLevel.Info);
            buf.Add("b", "", LogLevel.Error);

            var res = new ClearLogsTool(buf).Invoke(new JObject());

            Assert.AreEqual(2, (int)res["cleared"]);
            Assert.AreEqual(0, buf.Get(null, 50).Count);
        }

        [Test]
        public void AssetCrud_RoundTrip()
        {
            // create_folder
            var created = new CreateFolderTool().Invoke(new JObject { ["parent"] = "Assets", ["name"] = "ShtlMcpT4Tmp" });
            Assert.AreEqual(TmpFolder, (string)created["path"], "create_folder вернул путь");
            Assert.IsTrue(AssetDatabase.IsValidFolder(TmpFolder));

            // положить текстовый ассет
            var notePath = TmpFolder + "/note.txt";
            File.WriteAllText(Path.GetFullPath(notePath), "hello mcp");
            AssetDatabase.ImportAsset(notePath);

            // find_assets в папке
            var found = new FindAssetsTool().Invoke(new JObject { ["filter"] = "note", ["folder"] = TmpFolder });
            var paths = ((JArray)found["assets"]).Select(a => (string)a["path"]).ToList();
            CollectionAssert.Contains(paths, notePath);

            // read_asset → содержимое
            var read = new ReadAssetTool().Invoke(new JObject { ["path"] = notePath });
            Assert.AreEqual("hello mcp", (string)read["content"]);

            // move_asset
            var moved = new MoveAssetTool().Invoke(new JObject { ["from"] = notePath, ["to"] = TmpFolder + "/note2.txt" });
            Assert.IsTrue((bool)moved["moved"]);
            Assert.IsTrue(File.Exists(Path.GetFullPath(TmpFolder + "/note2.txt")));

            // delete_asset
            var del = new DeleteAssetTool().Invoke(new JObject { ["path"] = TmpFolder + "/note2.txt" });
            Assert.IsTrue((bool)del["deleted"]);
        }

        [Test]
        public void ReadAsset_MissingPath_Error()
        {
            var res = new ReadAssetTool().Invoke(new JObject { ["path"] = "Assets/__nope__.txt" });
            StringAssert.Contains("no asset", (string)res["error"]);
        }

        [Test]
        public void CreateFolder_BadParent_Error()
        {
            var res = new CreateFolderTool().Invoke(new JObject { ["parent"] = "Assets/__missing__", ["name"] = "x" });
            Assert.IsNotNull(res["error"]);
        }

        /// Полный путь, ради которого тул и заведён: материала на диске нет, создаём его и
        /// довешиваем текстуру — без единого Editor-скрипта в host-проекте.
        [Test]
        public void CreateAsset_Material_ThenModifyObjectSetsTexture()
        {
            new CreateFolderTool().Invoke(new JObject { ["parent"] = "Assets", ["name"] = "ShtlMcpT4Tmp" });
            var matPath = TmpFolder + "/mat.mat";
            var texPath = TmpFolder + "/tex.asset";

            var created = new CreateAssetTool().Invoke(new JObject
            {
                ["path"] = matPath,
                ["type"] = "Material",
                ["shader"] = "Unlit/Texture"
            });
            Assert.IsNull(created["error"], created.ToString());
            var material = AssetDatabase.LoadAssetAtPath<UnityEngine.Material>(matPath);
            Assert.IsNotNull(material, "материал создан на диске");
            Assert.AreEqual("Unlit/Texture", material.shader.name);

            // Поля созданного ассета правит modify_object — тулу остаётся только создание.
            new CreateAssetTool().Invoke(new JObject { ["path"] = texPath, ["type"] = "ShtlM5TestConfig" });
            var res = new ModifyObjectTool().Invoke(new JObject
            {
                ["target"] = texPath,
                ["property"] = "number",
                ["value"] = 5
            });
            Assert.IsNull(res["error"], res.ToString());
            Assert.AreEqual(5, AssetDatabase.LoadAssetAtPath<ShtlM5TestConfig>(texPath).number);
        }

        [Test]
        public void CreateAsset_ExistingPath_ErrorUnlessOverwrite()
        {
            new CreateFolderTool().Invoke(new JObject { ["parent"] = "Assets", ["name"] = "ShtlMcpT4Tmp" });
            var soPath = TmpFolder + "/config.asset";
            var args = new JObject { ["path"] = soPath, ["type"] = "ShtlM5TestConfig" };

            Assert.IsNull(new CreateAssetTool().Invoke((JObject)args.DeepClone())["error"]);

            var again = new CreateAssetTool().Invoke((JObject)args.DeepClone());
            StringAssert.Contains("already exists", (string)again["error"]);

            args["overwrite"] = true;
            Assert.IsNull(new CreateAssetTool().Invoke(args)["error"], "overwrite=true пересоздаёт");
        }

        [Test]
        public void CreateAsset_MaterialWithoutShader_Error()
        {
            new CreateFolderTool().Invoke(new JObject { ["parent"] = "Assets", ["name"] = "ShtlMcpT4Tmp" });
            var res = new CreateAssetTool().Invoke(new JObject
            {
                ["path"] = TmpFolder + "/noshader.mat",
                ["type"] = "Material"
            });
            StringAssert.Contains("'shader' is required", (string)res["error"]);
        }

        [Test]
        public void CreateAsset_PathWithoutExtension_Error()
        {
            var res = new CreateAssetTool().Invoke(new JObject
            {
                ["path"] = TmpFolder + "/noext",
                ["type"] = "ShtlM5TestConfig"
            });
            StringAssert.Contains("needs an extension", (string)res["error"]);
        }
    }
}
