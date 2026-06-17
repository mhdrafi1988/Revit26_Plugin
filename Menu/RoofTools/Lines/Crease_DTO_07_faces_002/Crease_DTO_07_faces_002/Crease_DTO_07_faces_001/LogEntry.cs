using System;

namespace Revit26_Plugin.CreaserAdv_V002.Services
{
    /// <summary>
    /// Represents one UI log entry.
    /// </summary>
    public sealed class LogEntry
    {
        public DateTime Timestamp { get; }
        public LoggingLevel Level { get; }
        public string Message { get; }

        public LogEntry(LoggingLevel level, string message)
        {
            Timestamp = DateTime.Now;
            Level = level;
            Message = message;
        }

        public override string ToString()
        {
            return $"[{Timestamp:HH:mm:ss}] [{Level}] {Message}";
        }
    }
}
