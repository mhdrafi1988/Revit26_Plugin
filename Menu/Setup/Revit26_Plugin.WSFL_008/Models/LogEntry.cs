using System;

namespace Revit26_Plugin.WSFL_008.Models
{
    public enum LogLevel
    {
        Info,
        Warning,
        Error
    }

    public class LogEntry
    {
        public DateTime Time { get; } = DateTime.Now;
        public LogLevel Level { get; }
        public string Message { get; }

        public LogEntry(LogLevel level, string message)
        {
            Level = level;
            Message = message;
        }

        public override string ToString()
        {
            return $"[{Time:HH:mm:ss}] {Level}: {Message}";
        }
    }
}
