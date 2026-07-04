using System.IO;
using NUnit.Framework;
using Newtonsoft.Json.Linq;
using UnityEditor;
using Shtl.Mcp.Jobs;
using Shtl.Mcp.Tools;

namespace Shtl.Mcp.Editor.Tests
{
    /// M5/AC3.9: write_asset — синхронные пути (некомпилируемые расширения, guard'ы, занятый
    /// reload-канал). Компиляционный путь (jobId через реальный reload) — WriteAssetReloadTests.
    /// Реальная FS + AssetDatabase, без моков.
    public class WriteAssetToolTests
    {
        const string JsonPath = "Assets/ShtlM5WriteAsset.json";
        const string DirRoot = "Assets/ShtlM5WaDir";
        const string CsNoImportPath = "Assets/ShtlM5NoImport.cs";

        WriteAssetTool Tool() => new WriteAssetTool(new JobStore("Shtl.Mcp.Tests.WriteAsset"), () => 0);

        [TearDown]
        public void TearDown()
        {
            AssetDatabase.DeleteAsset(JsonPath);
            AssetDatabase.DeleteAsset(DirRoot);
            // .cs писался с refresh=false → не импортирован, чистим с диска напрямую (без .meta)
            var full = Path.GetFullPath(CsNoImportPath);
            if (File.Exists(full))
            {
                File.Delete(full);
            }
            SessionState.EraseString(ReloadJobs.MarkerKey);
        }

        [Test]
        public void Write_Json_RoundTrip_AndOverwrite()
        {
            var res = Tool().Invoke(new JObject { ["path"] = JsonPath, ["content"] = "{\"a\":1}" });
            Assert.IsTrue((bool)res["written"], res.ToString());
            Assert.IsTrue((bool)res["created"], "первый write — created");
            Assert.AreEqual("{\"a\":1}", File.ReadAllText(Path.GetFullPath(JsonPath)));

            var res2 = Tool().Invoke(new JObject { ["path"] = JsonPath, ["content"] = "{\"a\":2}" });
            Assert.IsTrue((bool)res2["written"]);
            Assert.IsFalse((bool)res2["created"], "перезапись — created=false");

            var read = new ReadAssetTool().Invoke(new JObject { ["path"] = JsonPath });
            Assert.AreEqual("{\"a\":2}", (string)read["content"], "read_asset видит перезаписанное (парность)");
        }

        [Test]
        public void Write_CreatesFolders()
        {
            var res = Tool().Invoke(new JObject { ["path"] = DirRoot + "/sub/x.json", ["content"] = "{}" });
            Assert.IsTrue((bool)res["written"], res.ToString());
            Assert.IsTrue(File.Exists(Path.GetFullPath(DirRoot + "/sub/x.json")));
        }

        [Test]
        public void Write_CreateFoldersFalse_MissingFolder_Error()
        {
            var res = Tool().Invoke(new JObject
            {
                ["path"] = DirRoot + "/sub/x.json",
                ["content"] = "{}",
                ["createFolders"] = false
            });
            StringAssert.Contains("createFolders", (string)res["error"]);
        }

        [Test]
        public void Write_OutsideAssets_Error()
        {
            StringAssert.Contains("Assets/", (string)Tool().Invoke(
                new JObject { ["path"] = "Packages/x.txt", ["content"] = "" })["error"]);
            StringAssert.Contains("Assets/", (string)Tool().Invoke(
                new JObject { ["path"] = "/tmp/x.txt", ["content"] = "" })["error"]);
        }

        [Test]
        public void Write_DotDotSegments_Error()
        {
            var res = Tool().Invoke(new JObject { ["path"] = "Assets/../evil.cs", ["content"] = "" });
            StringAssert.Contains("..", (string)res["error"]);
        }

        [Test]
        public void Write_MissingContent_Error()
        {
            var res = Tool().Invoke(new JObject { ["path"] = JsonPath });
            StringAssert.Contains("content", (string)res["error"]);
        }

        [Test]
        public void Write_CompiledExt_RefreshFalse_WritesSyncWithoutJob()
        {
            var res = Tool().Invoke(new JObject
            {
                ["path"] = CsNoImportPath,
                ["content"] = "// m5 write_asset probe (не импортируется)",
                ["refresh"] = false
            });
            Assert.IsTrue((bool)res["written"], res.ToString());
            Assert.IsNull(res["jobId"], "refresh=false → синхронно, без job");
            Assert.IsTrue(File.Exists(Path.GetFullPath(CsNoImportPath)));
        }

        [Test]
        public void Write_CompiledExt_BusyReloadChannel_Error_NothingWritten()
        {
            // занять reload-job-канал (recompile/set_play_mode/write_asset делят его)
            SessionState.SetString(ReloadJobs.MarkerKey, "{\"jobId\":\"x\",\"kind\":\"recompile\"}");
            var res = Tool().Invoke(new JObject
            {
                ["path"] = CsNoImportPath,
                ["content"] = "// не должно записаться",
                ["refresh"] = true
            });
            StringAssert.Contains("already in progress", (string)res["error"]);
            Assert.IsFalse(File.Exists(Path.GetFullPath(CsNoImportPath)), "ошибка канала — без побочных эффектов");
        }
    }
}
