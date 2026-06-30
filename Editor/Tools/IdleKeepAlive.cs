using UnityEditor;

namespace Shtl.Mcp.Tools
{
    /// idle-keepalive (F4/AC4.10): пока сервер включён и тогл активен — держит редактор в No-Throttling
    /// (full-rate `EditorApplication.update`), чтобы в фоне (окно не в фокусе + простой) главный поток
    /// продолжал тикать и main-thread-инструменты + control-flag оставались доступны. Без этого глубокий idle
    /// заклинивает оба (оба висят на `update`); `ping` (AC4.8) затык лишь детектит, но не будит.
    ///
    /// Best-effort: подавление именно ФОНОВОГО троттла версионно-зависимо (Unity LTS) — источник истины о
    /// фактическом затыке остаётся `ping`. Переиспользует prefs/`ForceApply` `TestRunnerNoThrottle` (то же
    /// no-throttle, но как постоянный floor). Config-агностичен: «нужно ли» (`wanted`) решает Lifecycle.
    public static class IdleKeepAlive
    {
        /// Привести no-throttle к желаемому. **Во время тест-прогона не вмешивается** — там состоянием владеет
        /// `TestRunnerNoThrottle` (per-run, с durable-бэкапом). Иначе: `wanted` → форс full-rate; `!wanted` →
        /// вернуть Default. Идемпотентно и дёшево (две `EditorPrefs.GetInt` + запись только при дрейфе) —
        /// безопасно звать с каждого watchdog-тика. Так keepalive переживает per-run Restore: на следующем
        /// тике (или после) переустанавливается.
        public static void Reconcile(bool wanted)
        {
            bool testRunActive = !string.IsNullOrEmpty(SessionState.GetString(RunTestsTool.JobMarkerKey, ""));
            if (testRunActive)
            {
                return; // прогон владеет no-throttle — не конфликтуем (иначе сломали бы full-rate прогона)
            }

            bool isNoThrottle =
                EditorPrefs.GetInt(TestRunnerNoThrottle.IdleKey, TestRunnerNoThrottle.IdleDefault) == TestRunnerNoThrottle.IdleNoThrottle
                && EditorPrefs.GetInt(TestRunnerNoThrottle.ModeKey, TestRunnerNoThrottle.ModeDefault) == TestRunnerNoThrottle.ModeNoThrottle;

            if (wanted && !isNoThrottle)
            {
                Set(TestRunnerNoThrottle.IdleNoThrottle, TestRunnerNoThrottle.ModeNoThrottle);
            }
            else if (!wanted && isNoThrottle)
            {
                Set(TestRunnerNoThrottle.IdleDefault, TestRunnerNoThrottle.ModeDefault);
            }
        }

        static void Set(int idle, int mode)
        {
            EditorPrefs.SetInt(TestRunnerNoThrottle.IdleKey, idle);
            EditorPrefs.SetInt(TestRunnerNoThrottle.ModeKey, mode);
            TestRunnerNoThrottle.ForceApply();
        }
    }
}
