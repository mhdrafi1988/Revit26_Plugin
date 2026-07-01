// ==================================
// File: LogEntry.cs
// Namespace: Revit26_Plugin.CreaserAdv_V003_01
// ==================================

using System;

namespace Revit26_Plugin.CreaserAdv_V003_01.Services
{
    /// <summary>Immutable record of a single UI log event.</summary>
    public sealed class LogEntry
    {
        public DateTime    Timestamp { get; }
        public LoggingLevel Level    { get; }
        public string      Message  { get; }

        public LogEntry(LoggingLevel level, string message)
        {
            Timestamp = DateTime.Now;
            Level     = level;
            Message   = message;
        }

        public override string ToString()
            => $"[{Timestamp:HH:mm:ss}] [{Level}] {Message}";
    }
}
