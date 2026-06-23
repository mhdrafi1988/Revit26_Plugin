using System;

namespace Revit26_Plugin.Shared.Models
{
    /// <summary>
    /// Single log row: timestamp, severity level, and message.
    /// Shared across all tools — WorksetManager, AutoSlopeByPoint,
    /// AddPointOnIntersections, etc.
    /// Replaces per-tool copies; reference only this class going forward.
    /// </summary>
    public class LogEntry
    {
        public DateTime Time { get; }
        public LogLevel Level { get; }
        public string Message { get; }

        public LogEntry(LogLevel level, string message)
        {
            Time = DateTime.Now;
            Level = level;
            Message = message;
        }

        /// <summary>Flat string for Copy Log / clipboard export.</summary>
        public override string ToString()
            => $"{Time:HH:mm:ss}  {Level,-7}  {Message}";
    }
}
