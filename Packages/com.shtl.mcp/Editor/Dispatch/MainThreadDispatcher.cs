using System;
using System.Collections.Concurrent;
using System.Threading;

namespace ShtlMcp.Dispatch
{
    /// Фоновый поток кладёт работу, главный поток Unity вызывает Drain() в EditorApplication.update.
    public sealed class MainThreadDispatcher
    {
        readonly ConcurrentQueue<Action> _queue = new ConcurrentQueue<Action>();

        public void Enqueue(Action action) => _queue.Enqueue(action);

        public void Drain()
        {
            while (_queue.TryDequeue(out var a))
            {
                try { a(); } catch { /* изоляция: одна упавшая работа не валит pump */ }
            }
        }

        public T RunOnMain<T>(Func<T> func, int timeoutMs)
        {
            using (var done = new ManualResetEventSlim(false))
            {
                T result = default;
                Exception error = null;
                Enqueue(() =>
                {
                    try { result = func(); }
                    catch (Exception e) { error = e; }
                    finally { done.Set(); }
                });
                if (!done.Wait(timeoutMs))
                {
                    throw new TimeoutException("Main thread did not drain within timeout (compiling?)");
                }
                if (error != null)
                {
                    throw error;
                }
                return result;
            }
        }
    }
}
