using System.IO;
using UnityEditor;

namespace Shtl.Mcp.Tools
{
    /// Для PlayMode-прогонов run_tests: форсит `DisableDomainReload` (вход в Play НЕ выгружает домен →
    /// listener и прогон не гибнут, прогон стабилен). Паттерн CoplayDev PlayModeOptionsGuard; структурно
    /// зеркалит проверенный TestRunnerNoThrottle.
    ///
    /// Бэкап исходных `enterPlayModeOptions` — ДВУХСЛОЙНЫЙ:
    ///  - SessionState — переживает domain reload;
    ///  - marker-файл в `Library/` — переживает КРАШ редактора (иначе редактор останется с форсированным
    ///    DisableDomainReload навсегда: enterPlayModeOptions durable в ProjectSettings).
    public static class PlayModeOptionsGuard
    {
        const string SsCaptured = "Shtl.Mcp.PlayModeOpts.Captured";
        const string SsPrevEnabled = "Shtl.Mcp.PlayModeOpts.PrevEnabled";
        const string SsPrevOptions = "Shtl.Mcp.PlayModeOpts.PrevOptions";

        static string MarkerPath => Path.Combine("Library", "ShtlMcpPlayModeOptsBackup.txt");

        /// Форсировать DisableDomainReload. Снимок исходного — один раз (guard), затем выставить опцию.
        public static void Apply()
        {
            if (!SessionState.GetBool(SsCaptured, false))
            {
                bool en = EditorSettings.enterPlayModeOptionsEnabled;
                int opts = (int)EditorSettings.enterPlayModeOptions;
                SessionState.SetBool(SsPrevEnabled, en);
                SessionState.SetInt(SsPrevOptions, opts);
                SessionState.SetBool(SsCaptured, true);
                WriteMarker(en, opts);
            }

            EditorSettings.enterPlayModeOptionsEnabled = true;
            EditorSettings.enterPlayModeOptions |= EnterPlayModeOptions.DisableDomainReload;
        }

        /// Вернуть исходные enterPlayModeOptions. Идемпотентно: без снимка — no-op. Чистит оба слоя.
        public static void Restore()
        {
            if (!TryReadBackup(out bool en, out int opts))
            {
                return;
            }
            EditorSettings.enterPlayModeOptionsEnabled = en;
            EditorSettings.enterPlayModeOptions = (EnterPlayModeOptions)opts;
            ClearBackup();
        }

        /// После загрузки домена/старта: runPending → держим DisableDomainReload; иначе откатываем
        /// осиротевший снимок (краш в середине PlayMode-прогона).
        public static void RecoverOnLoad(bool runPending)
        {
            if (runPending)
            {
                EditorSettings.enterPlayModeOptionsEnabled = true;
                EditorSettings.enterPlayModeOptions |= EnterPlayModeOptions.DisableDomainReload;
                return;
            }
            if (HasBackup())
            {
                Restore();
            }
        }

        internal static bool HasBackup() =>
            SessionState.GetBool(SsCaptured, false) || File.Exists(MarkerPath);

        static bool TryReadBackup(out bool enabled, out int options)
        {
            if (SessionState.GetBool(SsCaptured, false))
            {
                enabled = SessionState.GetBool(SsPrevEnabled, false);
                options = SessionState.GetInt(SsPrevOptions, 0);
                return true;
            }

            enabled = false;
            options = 0;
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
                if (!int.TryParse(lines[0].Trim(), out int en) || !int.TryParse(lines[1].Trim(), out options))
                {
                    return false;
                }
                enabled = en != 0;
                return true;
            }
            catch
            {
                return false;
            }
        }

        static void WriteMarker(bool enabled, int options)
        {
            try
            {
                File.WriteAllText(MarkerPath, (enabled ? 1 : 0) + "\n" + options);
            }
            catch
            {
                // диск недоступен — остаётся слой SessionState
            }
        }

        internal static void ClearBackup()
        {
            SessionState.EraseBool(SsCaptured);
            SessionState.EraseBool(SsPrevEnabled);
            SessionState.EraseInt(SsPrevOptions);
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
            SessionState.EraseBool(SsPrevEnabled);
            SessionState.EraseInt(SsPrevOptions);
        }
    }
}
