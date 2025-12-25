using System;

namespace Revit26_Plugin.Creaser_V100.Models
{
    /// <summary>
    /// Represents a single log entry displayed in the UI log panel.
    /// </summary>
    public class LogEntry
    {
        public DateTime Timestamp { get; }
        public string Level { get; }
        public string Source { get; }
        public string Message { get; }

        public LogEntry(
            string level,
            string source,
            string message)
        {
            Timestamp = DateTime.Now;
            Level = level;
            Source = source;
            Message = message;
        }

        public override string ToString()
        {
            return $"[{Timestamp:HH:mm:ss}] [{Level}] [{Source}] {Message}";
        }
    }
}
