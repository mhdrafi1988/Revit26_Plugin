using System;

namespace Revit26_Plugin.Shared.Models
{
    /// <summary>
    /// Single log row: timestamp, severity level, and message.
    /// Shared across all tools — WorksetManager, AutoSlopeByPoint,
    /// RoofRidgeLines, DivideInnerLoops, AddPointOnIntersections, etc.
    /// Replaces per-tool copies; reference only this class going forward.
    /// </summary>
    public class LogEntry
    {
        /// <summary>Capture time when the log entry was created.</summary>
        public DateTime Time { get; }

        /// <summary>Severity level of this entry (Info, Warning, Error, Success).</summary>
        public LogLevel Level { get; }

        /// <summary>Log message text.</summary>
        public string Message { get; }

        /// <summary>
        /// Initializes a new log entry with the current time, specified level, and message.
        /// </summary>
        /// <param name="level">Severity level (Info, Warning, Error, Success).</param>
        /// <param name="message">Log message text.</param>
        public LogEntry(LogLevel level, string message)
        {
            Time = DateTime.Now;
            Level = level;
            Message = message;
        }

        /// <summary>Flat string format for Copy Log / clipboard export. Format: HH:mm:ss  Level  Message</summary>
        public override string ToString()
            => $"{Time:HH:mm:ss}  {Level,-7}  {Message}";
    }
}
