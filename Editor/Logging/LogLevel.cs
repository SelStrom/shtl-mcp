namespace Shtl.Mcp.Logging
{
    public enum LogLevel { Info = 0, Warning = 1, Error = 2 }

    public struct LogItem
    {
        public string Message;
        public string Stack;
        public LogLevel Level;
        public LogItem(string message, string stack, LogLevel level)
        { Message = message; Stack = stack; Level = level; }
    }
}
