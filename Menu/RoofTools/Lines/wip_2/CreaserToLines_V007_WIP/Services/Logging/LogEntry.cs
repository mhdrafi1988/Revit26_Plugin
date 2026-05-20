using System;

namespace Revit26_Plugin.CreaserAdv_V00.Services.Logging
{
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
            => $"[{Timestamp:HH:mm:ss}] [{Level}] {Message}";
    }
}
