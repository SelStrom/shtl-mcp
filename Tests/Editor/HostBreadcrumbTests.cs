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

        [Test] public void TargetPath_IsClaudeMdAtRoot()
        {
            var p = HostBreadcrumb.TargetPath(Path.Combine("foo", "bar"));
            StringAssert.EndsWith("CLAUDE.md", p);
            StringAssert.Contains("bar", p);
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
