using System;
using Newtonsoft.Json.Linq;

namespace Shtl.Mcp.Tools
{
    /// bg-liveness (AC4.8): отвечает с ФОНОВОГО потока (NeedsMainThread=false) — жив даже когда главный
    /// поток заблокирован (модальный диалог / компиляция / тяжёлая операция). `mainThreadAgeSeconds` —
    /// как давно тикал главный поток (дренаж dispatcher'а). Большой возраст при живом ответе = главный
    /// поток завис, а НЕ сервер мёртв. Отличает «модал-блок» от «down» (когда status молчит).
    public sealed class PingTool : ITool
    {
        const double WedgedThreshold = 5.0; // сек без дренажа → подозрение на блок главного потока

        readonly Func<DateTime> _lastDrainUtc;
        readonly Func<double> _listenerUptime;

        public PingTool(Func<DateTime> lastDrainUtc, Func<double> listenerUptime)
        {
            _lastDrainUtc = lastDrainUtc;
            _listenerUptime = listenerUptime;
        }

        public string Name => "ping";

        public string Description =>
            "Liveness probe served WITHOUT the main thread — responds even when the main thread is blocked " +
            "(modal dialog, compiling, heavy op). A large mainThreadAgeSeconds with a live response means the " +
            "main thread is wedged (NOT that the server is dead); main-thread tools like status will time out.";

        public bool NeedsMainThread => false; // ключевое: исполняется на фоновом HTTP-потоке

        public JObject InputSchema => new JObject { ["type"] = "object", ["properties"] = new JObject() };

        public JObject Invoke(JObject args)
        {
            double age = (DateTime.UtcNow - _lastDrainUtc()).TotalSeconds;
            if (age < 0)
            {
                age = 0;
            }
            var o = new JObject
            {
                ["alive"] = true,
                ["mainThreadAgeSeconds"] = Math.Round(age, 1),
                ["listenerUptimeSeconds"] = (int)_listenerUptime(),
                ["mainThreadResponsive"] = age < WedgedThreshold
            };
            if (age >= WedgedThreshold)
            {
                o["note"] = "main thread has not ticked for ~" + Math.Round(age, 1) +
                    "s — likely blocked (modal dialog / compiling / heavy op). The server is alive; " +
                    "main-thread tools (e.g. status) time out until it frees.";
            }
            return o;
        }
    }
}
