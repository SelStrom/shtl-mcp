namespace Shtl.Mcp.Jobs
{
    /// Единица async-работы. Сериализуема (Newtonsoft) для персиста в SessionState — переживает reload.
    public sealed class Job
    {
        public string Id;
        public string Tool;
        public string Status;        // "running" | "done" | "failed"
        public string Result;        // JSON-строка результата (для done)
        public string Error;         // текст ошибки (для failed)
        public long StartedAtTicks;  // DateTime.UtcNow.Ticks на момент создания

        // Прогресс (best-effort, in-memory, не персистится — транзиентно для running run_tests).
        public int Completed;
        public int Total;
        public string CurrentTest;
    }
}
