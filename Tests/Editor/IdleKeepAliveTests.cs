using NUnit.Framework;
using UnityEditor;
using Shtl.Mcp.Tools;

namespace Shtl.Mcp.Editor.Tests
{
    /// idle-keepalive (AC4.10): Reconcile приводит no-throttle к желаемому вне прогона, не вмешивается во
    /// время прогона, и переустанавливает keepalive после per-run Restore. Мутирует РЕАЛЬНЫЕ EditorPrefs
    /// (InteractionMode/ApplicationIdleTime) — снимок/восстановление в OneTime-хуках (как no-throttle-тесты).
    public class IdleKeepAliveTests
    {
        TestRunnerNoThrottle.StateSnapshot _ntSnap;
        string _liveMarker;

        [OneTimeSetUp]
        public void OneTimeSetUp()
        {
            _ntSnap = TestRunnerNoThrottle.Snapshot();
            _liveMarker = SessionState.GetString(RunTestsTool.JobMarkerKey, "");
        }

        [OneTimeTearDown]
        public void OneTimeTearDown()
        {
            if (string.IsNullOrEmpty(_liveMarker))
            {
                SessionState.EraseString(RunTestsTool.JobMarkerKey);
            }
            else
            {
                SessionState.SetString(RunTestsTool.JobMarkerKey, _liveMarker);
            }
            TestRunnerNoThrottle.RestoreSnapshot(_ntSnap);
        }

        [SetUp]
        public void SetUp()
        {
            SessionState.EraseString(RunTestsTool.JobMarkerKey); // прогон не активен
            SetPrefs(TestRunnerNoThrottle.IdleDefault, TestRunnerNoThrottle.ModeDefault); // старт с Default
        }

        [Test] public void Reconcile_Wanted_FromDefault_ForcesNoThrottle()
        {
            IdleKeepAlive.Reconcile(true);
            AssertPrefs(TestRunnerNoThrottle.IdleNoThrottle, TestRunnerNoThrottle.ModeNoThrottle);
        }

        [Test] public void Reconcile_NotWanted_FromNoThrottle_ReturnsDefault()
        {
            SetPrefs(TestRunnerNoThrottle.IdleNoThrottle, TestRunnerNoThrottle.ModeNoThrottle);
            IdleKeepAlive.Reconcile(false);
            AssertPrefs(TestRunnerNoThrottle.IdleDefault, TestRunnerNoThrottle.ModeDefault);
        }

        [Test] public void Reconcile_DuringTestRun_DoesNotTouch()
        {
            SessionState.SetString(RunTestsTool.JobMarkerKey, "fake-run"); // прогон владеет no-throttle
            SetPrefs(TestRunnerNoThrottle.IdleNoThrottle, TestRunnerNoThrottle.ModeNoThrottle);
            IdleKeepAlive.Reconcile(false); // даже !wanted — не должны отпускать во время прогона
            AssertPrefs(TestRunnerNoThrottle.IdleNoThrottle, TestRunnerNoThrottle.ModeNoThrottle);
        }

        [Test] public void Reconcile_Wanted_Idempotent_NoChange()
        {
            SetPrefs(TestRunnerNoThrottle.IdleNoThrottle, TestRunnerNoThrottle.ModeNoThrottle);
            IdleKeepAlive.Reconcile(true);
            AssertPrefs(TestRunnerNoThrottle.IdleNoThrottle, TestRunnerNoThrottle.ModeNoThrottle);
        }

        // RED-gate: keepalive обязан переустановиться после per-run Restore, вернувшего prefs к Default.
        // ClearBackup — детерминизм: живой прогон сьюта уже держит no-throttle-снимок, иначе Apply не захватил бы
        // свежий Default. OneTimeTearDown восстановит живое состояние через RestoreSnapshot.
        [Test] public void KeepAlive_ReassertsAfterTestRunnerRestore()
        {
            TestRunnerNoThrottle.ClearBackup();
            TestRunnerNoThrottle.Apply();   // captures Default(4/0), sets 0/1
            AssertPrefs(TestRunnerNoThrottle.IdleNoThrottle, TestRunnerNoThrottle.ModeNoThrottle);
            TestRunnerNoThrottle.Restore(); // reverts to backup 4/0 (per-run no-throttle снят)
            AssertPrefs(TestRunnerNoThrottle.IdleDefault, TestRunnerNoThrottle.ModeDefault);

            IdleKeepAlive.Reconcile(true);  // keepalive хотел full-rate → переустанавливает после Restore
            AssertPrefs(TestRunnerNoThrottle.IdleNoThrottle, TestRunnerNoThrottle.ModeNoThrottle);
        }

        static void SetPrefs(int idle, int mode)
        {
            EditorPrefs.SetInt(TestRunnerNoThrottle.IdleKey, idle);
            EditorPrefs.SetInt(TestRunnerNoThrottle.ModeKey, mode);
        }

        static void AssertPrefs(int idle, int mode)
        {
            Assert.AreEqual(idle, EditorPrefs.GetInt(TestRunnerNoThrottle.IdleKey, -1), "ApplicationIdleTime");
            Assert.AreEqual(mode, EditorPrefs.GetInt(TestRunnerNoThrottle.ModeKey, -1), "InteractionMode");
        }
    }
}
