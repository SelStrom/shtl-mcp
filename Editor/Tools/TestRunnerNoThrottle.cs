using System.IO;
using System.Reflection;
using UnityEditor;

namespace Shtl.Mcp.Tools
{
    /// Снимает focus-throttle редактора на время прогона тестов. В фоне (Unity не в фокусе) Unity
    /// троттлит `EditorApplication.update` → тест-раннер ползёт, а dispatcher не дренажится → сервер
    /// неотзывчив (status/get_job уходят в таймаут). Форсим full-rate через EditorPrefs
    /// `ApplicationIdleTime=0` + `InteractionMode=1` («No Throttling»). Паттерн — CoplayDev/unity-mcp.
    ///
    /// Бэкап исходных значений — ДВУХСЛОЙНЫЙ:
    ///  - SessionState — переживает domain reload (PlayMode-прогон, перекомпиляция в середине);
    ///  - marker-файл в `Library/` — переживает КРАШ редактора.
    /// Второй слой здесь важнее, чем в prior-art: EditorPrefs durable (переживают перезапуск редактора),
    /// поэтому если снимок только в SessionState, то после краша редактор навсегда остаётся в no-throttle
    /// И исходные значения потеряны. Диск это закрывает — следующая сессия откатит из marker'а.
    public static class TestRunnerNoThrottle
    {
        internal const string IdleKey = "ApplicationIdleTime"; // EditorPref: мс простоя между update; 0 = full-rate
        internal const string ModeKey = "InteractionMode";     // EditorPref: 0=Default, 1=No Throttling

        // internal: переиспользуются IdleKeepAlive (то же no-throttle, но как постоянный floor, не per-run).
        internal const int IdleDefault = 4;
        internal const int ModeDefault = 0;
        internal const int IdleNoThrottle = 0;
        internal const int ModeNoThrottle = 1;

        const string SsCaptured = "Shtl.Mcp.NoThrottle.Captured";
        const string SsPrevIdle = "Shtl.Mcp.NoThrottle.PrevIdle";
        const string SsPrevMode = "Shtl.Mcp.NoThrottle.PrevMode";

        // project-local, по умолчанию в .gitignore — не попадает в VCS.
        static string MarkerPath => Path.Combine("Library", "ShtlMcpNoThrottleBackup.txt");

        /// Снять троттлинг. Снимок исходных значений — ОДИН раз (guard `Captured`), затем full-rate.
        public static void Apply()
        {
            if (!SessionState.GetBool(SsCaptured, false))
            {
                int idle = EditorPrefs.GetInt(IdleKey, IdleDefault);
                int mode = EditorPrefs.GetInt(ModeKey, ModeDefault);
                SessionState.SetInt(SsPrevIdle, idle);
                SessionState.SetInt(SsPrevMode, mode);
                SessionState.SetBool(SsCaptured, true);
                WriteMarker(idle, mode);
            }

            EditorPrefs.SetInt(IdleKey, IdleNoThrottle);
            EditorPrefs.SetInt(ModeKey, ModeNoThrottle);
            ForceApply();
        }

        /// Вернуть троттлинг к исходному. Идемпотентно: без снимка — no-op. Чистит оба слоя бэкапа.
        public static void Restore()
        {
            if (!TryReadBackup(out int idle, out int mode))
            {
                return;
            }
            EditorPrefs.SetInt(IdleKey, idle);
            EditorPrefs.SetInt(ModeKey, mode);
            ForceApply();
            ClearBackup();
        }

        /// Восстановление после загрузки домена / старта редактора.
        ///  runPending=true  — прогон ещё в полёте (PlayMode reload и т.п.): держим no-throttle full-rate;
        ///  runPending=false — снимок остался без активного прогона (краш/прерывание): откатываем.
        public static void RecoverOnLoad(bool runPending)
        {
            if (runPending)
            {
                EditorPrefs.SetInt(IdleKey, IdleNoThrottle);
                EditorPrefs.SetInt(ModeKey, ModeNoThrottle);
                ForceApply();
                return;
            }
            if (HasBackup())
            {
                Restore();
            }
        }

        internal static bool HasBackup() =>
            SessionState.GetBool(SsCaptured, false) || File.Exists(MarkerPath);

        // SessionState (быстрый путь, переживает reload) → marker-файл (переживает краш).
        static bool TryReadBackup(out int idle, out int mode)
        {
            if (SessionState.GetBool(SsCaptured, false))
            {
                idle = SessionState.GetInt(SsPrevIdle, IdleDefault);
                mode = SessionState.GetInt(SsPrevMode, ModeDefault);
                return true;
            }

            idle = IdleDefault;
            mode = ModeDefault;
            try
            {
                if (!File.Exists(MarkerPath))
                {
                    return false;
                }
                var lines = File.ReadAllLines(MarkerPath);
                if (lines.Length < 2)
                {
                    return false;
                }
                return int.TryParse(lines[0].Trim(), out idle) && int.TryParse(lines[1].Trim(), out mode);
            }
            catch
            {
                return false;
            }
        }

        static void WriteMarker(int idle, int mode)
        {
            try
            {
                File.WriteAllText(MarkerPath, idle + "\n" + mode);
            }
            catch
            {
                // диск недоступен — остаётся слой SessionState (краш-защиту теряем, reload-защиту нет)
            }
        }

        internal static void ClearBackup()
        {
            SessionState.EraseBool(SsCaptured);
            SessionState.EraseInt(SsPrevIdle);
            SessionState.EraseInt(SsPrevMode);
            try
            {
                if (File.Exists(MarkerPath))
                {
                    File.Delete(MarkerPath);
                }
            }
            catch
            {
                // ignored
            }
        }

        // Тест-хук: симуляция краша — сбросить только SessionState-слой, оставив marker на диске.
        internal static void DropSessionLayer()
        {
            SessionState.EraseBool(SsCaptured);
            SessionState.EraseInt(SsPrevIdle);
            SessionState.EraseInt(SsPrevMode);
        }

        // Полный снимок глобального no-throttle-состояния (prefs + оба слоя бэкапа). Нужен тестам:
        // прогон через MCP run_tests УЖЕ применил no-throttle, поэтому тесты, мутирующие это состояние,
        // обязаны вернуть его в точности — иначе live-RunFinished не восстановит троттлинг.
        internal sealed class StateSnapshot
        {
            public int LiveIdle, LiveMode;
            public bool Captured;
            public int PrevIdle, PrevMode;
            public bool MarkerExists;
            public string MarkerText;
        }

        internal static StateSnapshot Snapshot()
        {
            var s = new StateSnapshot
            {
                LiveIdle = EditorPrefs.GetInt(IdleKey, IdleDefault),
                LiveMode = EditorPrefs.GetInt(ModeKey, ModeDefault),
                Captured = SessionState.GetBool(SsCaptured, false),
                PrevIdle = SessionState.GetInt(SsPrevIdle, IdleDefault),
                PrevMode = SessionState.GetInt(SsPrevMode, ModeDefault),
                MarkerExists = File.Exists(MarkerPath)
            };
            if (s.MarkerExists)
            {
                try { s.MarkerText = File.ReadAllText(MarkerPath); } catch { s.MarkerExists = false; }
            }
            return s;
        }

        internal static void RestoreSnapshot(StateSnapshot s)
        {
            EditorPrefs.SetInt(IdleKey, s.LiveIdle);
            EditorPrefs.SetInt(ModeKey, s.LiveMode);
            if (s.Captured)
            {
                SessionState.SetBool(SsCaptured, true);
                SessionState.SetInt(SsPrevIdle, s.PrevIdle);
                SessionState.SetInt(SsPrevMode, s.PrevMode);
            }
            else
            {
                SessionState.EraseBool(SsCaptured);
                SessionState.EraseInt(SsPrevIdle);
                SessionState.EraseInt(SsPrevMode);
            }
            try
            {
                if (s.MarkerExists && s.MarkerText != null)
                {
                    File.WriteAllText(MarkerPath, s.MarkerText);
                }
                else if (File.Exists(MarkerPath))
                {
                    File.Delete(MarkerPath);
                }
            }
            catch
            {
                // ignored
            }
            ForceApply();
        }

        // EditorPrefs-записи не вступают в силу, пока редактор не перечитает interaction-настройки.
        // Приватный static `EditorApplication.UpdateInteractionModeSettings()` — load-bearing (prior-art).
        // internal: единственная точка рефлексии, переиспользуется IdleKeepAlive (не дублировать).
        internal static void ForceApply()
        {
            try
            {
                typeof(EditorApplication)
                    .GetMethod("UpdateInteractionModeSettings", BindingFlags.Static | BindingFlags.NonPublic)
                    ?.Invoke(null, null);
            }
            catch
            {
                // рефлексия недоступна на этой версии — prefs применятся при следующем чтении настроек
            }
        }
    }
}
