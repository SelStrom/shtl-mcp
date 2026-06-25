using NUnit.Framework;
using UnityEditor;
using Shtl.Mcp.Tools;

namespace Shtl.Mcp.Editor.Tests
{
    /// T5: двухслойный бэкап enterPlayModeOptions (Apply→DisableDomainReload→Restore; crash-recovery с диска).
    /// Реальные EditorSettings снимаем/возвращаем в OneTime (EditMode-прогон guard не использует → коллизий нет).
    public class PlayModeOptionsGuardTests
    {
        bool _realEnabled;
        EnterPlayModeOptions _realOptions;

        [OneTimeSetUp]
        public void OneTimeSetUp()
        {
            _realEnabled = EditorSettings.enterPlayModeOptionsEnabled;
            _realOptions = EditorSettings.enterPlayModeOptions;
        }

        [OneTimeTearDown]
        public void OneTimeTearDown()
        {
            EditorSettings.enterPlayModeOptionsEnabled = _realEnabled;
            EditorSettings.enterPlayModeOptions = _realOptions;
            PlayModeOptionsGuard.ClearBackup();
        }

        [SetUp]
        public void SetUp()
        {
            PlayModeOptionsGuard.ClearBackup();
            EditorSettings.enterPlayModeOptionsEnabled = false;
            EditorSettings.enterPlayModeOptions = EnterPlayModeOptions.None;
        }

        [TearDown]
        public void TearDown()
        {
            PlayModeOptionsGuard.Restore();
            PlayModeOptionsGuard.ClearBackup();
        }

        [Test]
        public void Apply_SetsDisableDomainReload()
        {
            PlayModeOptionsGuard.Apply();
            Assert.IsTrue(EditorSettings.enterPlayModeOptionsEnabled);
            Assert.IsTrue((EditorSettings.enterPlayModeOptions & EnterPlayModeOptions.DisableDomainReload) != 0);
        }

        [Test]
        public void Restore_RevertsToBaseline()
        {
            PlayModeOptionsGuard.Apply();
            PlayModeOptionsGuard.Restore();
            Assert.IsFalse(EditorSettings.enterPlayModeOptionsEnabled);
            Assert.AreEqual(EnterPlayModeOptions.None, EditorSettings.enterPlayModeOptions);
            Assert.IsFalse(PlayModeOptionsGuard.HasBackup());
        }

        // Краш в середине PlayMode-прогона: SessionState мёртв, но enterPlayModeOptions durable остался
        // форсированным. Disk-слой обязан восстановить исходное.
        [Test]
        public void Restore_FromDiskLayer_AfterSessionLoss()
        {
            PlayModeOptionsGuard.Apply();
            PlayModeOptionsGuard.DropSessionLayer();
            Assert.IsTrue(PlayModeOptionsGuard.HasBackup(), "disk-слой переживает потерю SessionState");

            PlayModeOptionsGuard.RecoverOnLoad(runPending: false);

            Assert.IsFalse(EditorSettings.enterPlayModeOptionsEnabled, "восстановлено с диска");
            Assert.AreEqual(EnterPlayModeOptions.None, EditorSettings.enterPlayModeOptions);
            Assert.IsFalse(PlayModeOptionsGuard.HasBackup());
        }

        [Test]
        public void RecoverOnLoad_RunPending_KeepsDisableDomainReload()
        {
            PlayModeOptionsGuard.Apply();
            EditorSettings.enterPlayModeOptionsEnabled = false; // как будто что-то сбросило
            EditorSettings.enterPlayModeOptions = EnterPlayModeOptions.None;

            PlayModeOptionsGuard.RecoverOnLoad(runPending: true);

            Assert.IsTrue((EditorSettings.enterPlayModeOptions & EnterPlayModeOptions.DisableDomainReload) != 0,
                "in-flight PlayMode-прогон → держим DisableDomainReload");
            Assert.IsTrue(PlayModeOptionsGuard.HasBackup());
        }
    }
}
