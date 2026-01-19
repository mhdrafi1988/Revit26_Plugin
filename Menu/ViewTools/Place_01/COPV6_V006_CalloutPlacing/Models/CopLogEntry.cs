using System;

namespace Revit26_Plugin.CalloutCOP_V06.Models
{
    public enum CopLogLevel
    {
        Info,
        Warning,
        Error
    }

    public sealed class CopLogEntry
    {
        public DateTime Timestamp { get; }
        public CopLogLevel Level { get; }
        public string Message { get; }

        public CopLogEntry(CopLogLevel level, string message)
        {
            Timestamp = DateTime.Now;
            Level = level;
            Message = message;
        }

        public override string ToString()
            => $"[{Timestamp:HH:mm:ss}] {Level}: {Message}";
    }
}
