using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;
using Shtl.Mcp.Logging;

namespace Shtl.Mcp.Lifecycle
{
    /// Ранняя, переживающая domain reload подписка на лог Unity.
    ///
    /// Подписка навешивается из `[InitializeOnLoad]`-ctor'а `ShtlMcpBootstrap` (`Install`) —
    /// синхронно на КАЖДОЙ загрузке домена, до delayCall-driven `EnsureStarted`. Поэтому в буфер
    /// попадают и стартовые логи редактора, и логи сразу после reload (раньше подписка жила в
    /// `EnsureStarted`, а он запускался только на первом тике update → всё до него терялось).
    ///
    /// Содержимое буфера сериализуется в `SessionState` в `beforeAssemblyReload` и восстанавливается
    /// в `Install` — так же, как JobStore переживает reload. Иначе новый домен = пустой буфер и
    /// `get_logs` «забывал» всё до последней перекомпиляции.
    ///
    /// Захват — только в главном редакторе: `Install` зовётся после guard'а
    /// `AssetDatabase.IsAssetImportWorkerProcess()` в bootstrap (у воркеров свой SessionState и
    /// свой лог — не Console редактора).
    public static class LogCapture
    {
        const string PersistKey = "Shtl.Mcp.LogBuffer";
        const int Capacity = 500;

        static bool _installed;

        public static LogBuffer Buffer { get; } = new LogBuffer(Capacity);

        public static void Install()
        {
            // Статики сбрасываются на каждом domain reload → _installed==false ⇒ Install отрабатывает
            // ровно один раз за домен. Флаг защищает от повторного Restore при двойном вызове в одном домене.
            if (_installed)
            {
                return;
            }
            _installed = true;

            Restore();

            Application.logMessageReceivedThreaded -= OnLog;
            Application.logMessageReceivedThreaded += OnLog;

            AssemblyReloadEvents.beforeAssemblyReload -= Persist;
            AssemblyReloadEvents.beforeAssemblyReload += Persist;
        }

        static void OnLog(string message, string stack, LogType type)
            => Buffer.Add(message, stack, ToLevel(type));

        static LogLevel ToLevel(LogType type)
            => type == LogType.Error || type == LogType.Exception || type == LogType.Assert ? LogLevel.Error
             : type == LogType.Warning ? LogLevel.Warning
             : LogLevel.Info;

        static void Persist() => SessionState.SetString(PersistKey, Serialize(Buffer.Snapshot()));

        static void Restore()
        {
            foreach (var it in Deserialize(SessionState.GetString(PersistKey, "")))
            {
                Buffer.Add(it.Message, it.Stack, it.Level);
            }
        }

        internal static string Serialize(IReadOnlyList<LogItem> items)
        {
            var arr = new JArray();
            foreach (var it in items)
            {
                arr.Add(new JObject { ["m"] = it.Message, ["s"] = it.Stack, ["l"] = (int)it.Level });
            }
            return arr.ToString(Formatting.None);
        }

        internal static List<LogItem> Deserialize(string json)
        {
            var result = new List<LogItem>();
            if (string.IsNullOrEmpty(json))
            {
                return result;
            }
            try
            {
                foreach (var t in JArray.Parse(json))
                {
                    result.Add(new LogItem((string)t["m"], (string)t["s"], (LogLevel)(int)t["l"]));
                }
            }
            catch (JsonException)
            {
                // Повреждённый снимок — начинаем с пустого, не роняем загрузку домена.
            }
            return result;
        }
    }
}
