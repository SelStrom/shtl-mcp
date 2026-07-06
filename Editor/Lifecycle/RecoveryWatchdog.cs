using System;
using System.Threading;

namespace Shtl.Mcp.Lifecycle
{
    /// Фоновый поток восстановления, НЕЗАВИСИМЫЙ от EditorApplication.update (тот троттлится без фокуса и
    /// замирает на модалке). Держит listener живым (ре-бинд), читает control-flag и пишет heartbeat, пока
    /// главный поток заблокирован. Только bg-safe работа: HttpListener bind + файловый IO, без UnityEngine/*.
    public sealed class RecoveryWatchdog
    {
        readonly Action _tick;
        readonly int _intervalMs;
        Thread _thread;
        volatile bool _running;

        public RecoveryWatchdog(Action tick, int intervalMs)
        {
            _tick = tick;
            _intervalMs = intervalMs;
        }

        public void Start()
        {
            if (_running)
            {
                return;
            }
            _running = true;
            _thread = new Thread(Loop) { IsBackground = true, Name = "ShtlMcpWatchdog" };
            _thread.Start();
        }

        void Loop()
        {
            while (_running)
            {
                try
                {
                    _tick();
                }
                catch
                {
                    // одиночный сбой тика не валит поток восстановления
                }
                Thread.Sleep(_intervalMs);
            }
        }

        public void Stop()
        {
            _running = false;
            _thread = null; // поток IsBackground — сам завершится по _running=false
        }
    }
}
