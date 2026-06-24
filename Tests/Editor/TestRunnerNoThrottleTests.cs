using NUnit.Framework;
using UnityEditor;
using Shtl.Mcp.Tools;

namespace Shtl.Mcp.Editor.Tests
{
    /// Поведение no-throttle: round-trip (snapshot→full-rate→restore), двухслойный бэкап
    /// (восстановление с диска после «потери» SessionState = эмуляция краша), recover при reload.
    ///
    /// Тесты мутируют ГЛОБАЛЬНОЕ no-throttle-состояние (EditorPrefs + бэкап). Прогон через MCP run_tests
    /// уже применил no-throttle вокруг сьюта, поэтому полный снимок live-состояния берём/возвращаем в
    /// OneTime-хуках — иначе RunFinished не восстановит троттлинг и редактор останется в no-throttle.
    public class TestRunnerNoThrottleTests
    {
        // Синтетический baseline (не дефолт 4/0, чтобы отличать от not-set).
        const int BaseIdle = 7;
        const int BaseMode = 0;

        TestRunnerNoThrottle.StateSnapshot _live;

        [OneTimeSetUp]
        public void OneTimeSetUp()
        {
            _live = TestRunnerNoThrottle.Snapshot();
        }

        [OneTimeTearDown]
        public void OneTimeTearDown()
        {
            TestRunnerNoThrottle.RestoreSnapshot(_live);
        }

        [SetUp]
        public void SetUp()
        {
            TestRunnerNoThrottle.ClearBackup();
            EditorPrefs.SetInt(TestRunnerNoThrottle.IdleKey, BaseIdle);
            EditorPrefs.SetInt(TestRunnerNoThrottle.ModeKey, BaseMode);
        }

        [TearDown]
        public void TearDown()
        {
            TestRunnerNoThrottle.Restore();   // вернуть к baseline, если снимок ещё активен
            TestRunnerNoThrottle.ClearBackup();
        }

        [Test]
        public void Apply_SetsFullRate()
        {
            TestRunnerNoThrottle.Apply();
            Assert.AreEqual(0, EditorPrefs.GetInt(TestRunnerNoThrottle.IdleKey, -1), "idle должен стать 0 (full-rate)");
            Assert.AreEqual(1, EditorPrefs.GetInt(TestRunnerNoThrottle.ModeKey, -1), "mode должен стать 1 (No Throttling)");
        }

        [Test]
        public void Restore_ReturnsToCapturedBaseline()
        {
            TestRunnerNoThrottle.Apply();
            TestRunnerNoThrottle.Restore();

            Assert.AreEqual(BaseIdle, EditorPrefs.GetInt(TestRunnerNoThrottle.IdleKey, -1));
            Assert.AreEqual(BaseMode, EditorPrefs.GetInt(TestRunnerNoThrottle.ModeKey, -1));
            Assert.IsFalse(TestRunnerNoThrottle.HasBackup(), "оба слоя бэкапа должны быть очищены");
        }

        // Краш в середине прогона: SessionState мёртв, но EditorPrefs durable остались в no-throttle.
        // Disk-слой обязан восстановить исходные значения — иначе редактор навсегда в no-throttle.
        [Test]
        public void Restore_FromDiskLayer_AfterSessionLoss()
        {
            TestRunnerNoThrottle.Apply();                 // snapshot в SessionState + Library/-marker, prefs → 0/1
            TestRunnerNoThrottle.DropSessionLayer();       // эмуляция краша: только SessionState-слой стёрт, marker на диске

            Assert.IsTrue(TestRunnerNoThrottle.HasBackup(), "disk-слой должен пережить потерю SessionState");

            TestRunnerNoThrottle.RecoverOnLoad(runPending: false); // следующая сессия: осиротевший снимок → откат

            Assert.AreEqual(BaseIdle, EditorPrefs.GetInt(TestRunnerNoThrottle.IdleKey, -1), "idle восстановлен с диска");
            Assert.AreEqual(BaseMode, EditorPrefs.GetInt(TestRunnerNoThrottle.ModeKey, -1), "mode восстановлен с диска");
            Assert.IsFalse(TestRunnerNoThrottle.HasBackup(), "marker должен быть удалён после восстановления");
        }

        // Прогон ещё в полёте после reload (PlayMode): no-throttle надо ДЕРЖАТЬ, не откатывать.
        [Test]
        public void RecoverOnLoad_RunPending_KeepsFullRate()
        {
            TestRunnerNoThrottle.Apply();
            EditorPrefs.SetInt(TestRunnerNoThrottle.IdleKey, BaseIdle); // как будто что-то сбросило троттлинг

            TestRunnerNoThrottle.RecoverOnLoad(runPending: true);

            Assert.AreEqual(0, EditorPrefs.GetInt(TestRunnerNoThrottle.IdleKey, -1), "no-throttle должен держаться при in-flight прогоне");
            Assert.IsTrue(TestRunnerNoThrottle.HasBackup(), "снимок не очищается, пока прогон не завершён");
        }
    }
}
