using System.Collections.Generic;
using System.Linq;

namespace Shtl.Mcp.Logging
{
    public sealed class LogBuffer
    {
        readonly int _cap;
        readonly LinkedList<LogItem> _items = new LinkedList<LogItem>();
        readonly object _lock = new object();

        public LogBuffer(int capacity) => _cap = capacity;

        public void Add(string message, string stack, LogLevel level)
        {
            lock (_lock)
            {
                _items.AddLast(new LogItem(message, stack, level));
                while (_items.Count > _cap)
                {
                    _items.RemoveFirst();
                }
            }
        }

        /// Очистить буфер. Возвращает число удалённых записей.
        public int Clear()
        {
            lock (_lock)
            {
                int n = _items.Count;
                _items.Clear();
                return n;
            }
        }

        /// Возвращает до count последних записей (в хронологическом порядке), c фильтром по min-уровню.
        public IReadOnlyList<LogItem> Get(LogLevel? min, int count)
        {
            lock (_lock)
            {
                IEnumerable<LogItem> q = _items;
                if (min.HasValue)
                {
                    q = q.Where(i => i.Level >= min.Value);
                }
                var all = q.ToList();
                int skip = all.Count > count ? all.Count - count : 0;
                return all.Skip(skip).ToList();
            }
        }
    }
}
