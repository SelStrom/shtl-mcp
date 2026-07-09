using System;
using System.IO;
using NUnit.Framework;
using Shtl.Mcp.Lifecycle;

namespace Shtl.Mcp.Editor.Tests
{
    /// F7/AC7.4 host-крошка: текст/маркер, идемпотентность, целевой путь, запись (реальная ФС, temp-файлы).
    public class HostBreadcrumbTests
    {
        [Test] public void Text_HasMarkerAndKeyPointers()
        {
            var t = HostBreadcrumb.Text();
            StringAssert.Contains(HostBreadcrumb.Marker, t);
            StringAssert.Contains("registry.json", t);
            StringAssert.Contains("ping", t);
        }

        [Test] public void IsPresent_DetectsMarker()
        {
            Assert.IsTrue(HostBreadcrumb.IsPresent("x " + HostBreadcrumb.Marker + " y"));
            Assert.IsFalse(HostBreadcrumb.IsPresent("no marker here"));
            Assert.IsFalse(HostBreadcrumb.IsPresent(""));
            Assert.IsFalse(HostBreadcrumb.IsPresent(null));
        }

        [Test] public void Resolve_NestedUnityProject_PicksRepoRootClaudeMd()
        {
            var repo = TempTree(out var unityRoot, gitAsFile: false);
            try
            {
                File.WriteAllText(Path.Combine(repo, "CLAUDE.md"), "# Repo\n");
                var p = HostBreadcrumb.ResolveTargetPath(unityRoot);
                Assert.AreEqual(Path.Combine(repo, "CLAUDE.md"), p);
            }
            finally { Directory.Delete(repo, true); }
        }

        [Test] public void Resolve_NoClaudeMdAnywhere_DefaultsToRepoRoot()
        {
            var repo = TempTree(out var unityRoot, gitAsFile: false);
            try
            {
                var p = HostBreadcrumb.ResolveTargetPath(unityRoot);
                Assert.AreEqual(Path.Combine(repo, "CLAUDE.md"), p, "файла нет нигде — создавать в git-корне");
            }
            finally { Directory.Delete(repo, true); }
        }

        [Test] public void Resolve_MarkerAtUnityRoot_WinsOverRepoRoot()
        {
            var repo = TempTree(out var unityRoot, gitAsFile: false);
            try
            {
                File.WriteAllText(Path.Combine(repo, "CLAUDE.md"), "# Repo без маркера\n");
                File.WriteAllText(Path.Combine(unityRoot, "CLAUDE.md"), "x " + HostBreadcrumb.Marker + "\n");
                var p = HostBreadcrumb.ResolveTargetPath(unityRoot);
                Assert.AreEqual(Path.Combine(unityRoot, "CLAUDE.md"), p, "крошка уже добавлена ранее — статус ✓, не дублировать в другом файле");
            }
            finally { Directory.Delete(repo, true); }
        }

        [Test] public void Resolve_ExistingOnlyAtUnityRoot_UsedWhenRepoRootHasNone()
        {
            var repo = TempTree(out var unityRoot, gitAsFile: false);
            try
            {
                File.WriteAllText(Path.Combine(unityRoot, "CLAUDE.md"), "# Unity host\n");
                var p = HostBreadcrumb.ResolveTargetPath(unityRoot);
                Assert.AreEqual(Path.Combine(unityRoot, "CLAUDE.md"), p, "дописывать в существующий файл, а не создавать новый");
            }
            finally { Directory.Delete(repo, true); }
        }

        [Test] public void Resolve_GitFile_TreatedAsRepoRoot()
        {
            var repo = TempTree(out var unityRoot, gitAsFile: true); // worktree/submodule: .git — файл
            try
            {
                var p = HostBreadcrumb.ResolveTargetPath(unityRoot);
                Assert.AreEqual(Path.Combine(repo, "CLAUDE.md"), p);
            }
            finally { Directory.Delete(repo, true); }
        }

        [Test] public void Resolve_NoGit_FallsBackToUnityProjectRoot()
        {
            var root = Path.Combine(Path.GetTempPath(), "shtl_bc_" + Guid.NewGuid().ToString("N"));
            var unityRoot = Path.Combine(root, "Client", "Unity");
            Directory.CreateDirectory(unityRoot);
            try
            {
                var p = HostBreadcrumb.ResolveTargetPath(unityRoot);
                Assert.AreEqual(Path.Combine(unityRoot, "CLAUDE.md"), p);
            }
            finally { Directory.Delete(root, true); }
        }

        /// repo/.git + repo/Client/Unity — типовая вложенность Unity-проекта в репозиторий.
        static string TempTree(out string unityProjectRoot, bool gitAsFile)
        {
            var repo = Path.Combine(Path.GetTempPath(), "shtl_bc_" + Guid.NewGuid().ToString("N"));
            unityProjectRoot = Path.Combine(repo, "Client", "Unity");
            Directory.CreateDirectory(unityProjectRoot);
            if (gitAsFile)
            {
                File.WriteAllText(Path.Combine(repo, ".git"), "gitdir: /elsewhere\n");
            }
            else
            {
                Directory.CreateDirectory(Path.Combine(repo, ".git"));
            }
            return repo;
        }

        [Test] public void AddTo_AppendsThenIdempotent()
        {
            var tmp = TempFile();
            try
            {
                File.WriteAllText(tmp, "# Host\n");
                Assert.IsTrue(HostBreadcrumb.AddTo(tmp), "первый раз — дописали");
                var after = File.ReadAllText(tmp);
                StringAssert.Contains("# Host", after, "исходное содержимое сохранено");
                StringAssert.Contains(HostBreadcrumb.Marker, after);

                Assert.IsFalse(HostBreadcrumb.AddTo(tmp), "повтор — no-op (маркер уже есть)");
                Assert.AreEqual(after, File.ReadAllText(tmp), "файл не изменился при повторе");
            }
            finally
            {
                File.Delete(tmp);
            }
        }

        [Test] public void AddTo_CreatesMissingFile()
        {
            var tmp = TempFile();
            File.Delete(tmp); // файла нет
            try
            {
                Assert.IsTrue(HostBreadcrumb.AddTo(tmp));
                Assert.IsTrue(File.Exists(tmp));
                StringAssert.Contains(HostBreadcrumb.Marker, File.ReadAllText(tmp));
            }
            finally
            {
                if (File.Exists(tmp))
                {
                    File.Delete(tmp);
                }
            }
        }

        static string TempFile()
        {
            return Path.Combine(Path.GetTempPath(), "shtl_bc_" + Guid.NewGuid().ToString("N") + ".md");
        }
    }
}
