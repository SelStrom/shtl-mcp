using System;
using System.Collections.Generic;

namespace Shtl.Mcp.Lifecycle
{
    /// Кольцевой буфер последних N MCP-вызовов для дашборда (F5/AC5.5). Транзиентный, in-memory.
    /// Пишется с фонового HTTP-потока (router), читается с главного (дашборд) → доступ под lock.
    public sealed class CallTail
    {
        public readonly struct Entry
        {
            public readonly long AtTicks;   // DateTime.UtcNow.Ticks момента вызова
            public readonly string Method;  // имя тула
            public readonly bool Ok;        // ✓ — без исключения и без error в результате
            public readonly double Ms;      // длительность обработки

            public Entry(long atTicks, string method, bool ok, double ms)
            {
                AtTicks = atTicks;
                Method = method;
                Ok = ok;
                Ms = ms;
            }
        }

        readonly int _cap;
        readonly LinkedList<Entry> _items = new LinkedList<Entry>();
        readonly object _lock = new object();

        public CallTail(int capacity = 20)
        {
            _cap = capacity;
        }

        public void Record(string method, bool ok, double ms)
        {
            var e = new Entry(DateTime.UtcNow.Ticks, method ?? "?", ok, ms);
            lock (_lock)
            {
                _items.AddLast(e);
                if (_items.Count > _cap)
                {
                    _items.RemoveFirst();
                }
            }
        }

        /// Снимок, новейшие-первыми (для рендера сверху вниз).
        public Entry[] Snapshot()
        {
            lock (_lock)
            {
                var arr = new Entry[_items.Count];
                int i = _items.Count - 1;
                foreach (var e in _items)
                {
                    arr[i--] = e;
                }
                return arr;
            }
        }
    }
}
