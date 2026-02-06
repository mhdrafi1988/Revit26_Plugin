using System;

namespace Revit26_Plugin.APUS_V313.ViewModels
{
    public enum LogLevel
    {
        Info,
        Warning,
        Error
    }

    /// <summary>
    /// Immutable log row for the Live Log panel.
    /// </summary>
    public class LogEntryViewModel
    {
        public DateTime Timestamp { get; }
        public LogLevel Level { get; }
        public string Message { get; }

        public string DisplayText => $"[{Timestamp:HH:mm:ss}] {Message}";

        // ? This is the constructor your ViewModel expects
        public LogEntryViewModel(LogLevel level, string message)
        {
            Timestamp = DateTime.Now;
            Level = level;
            Message = message ?? string.Empty;
        }
    }
}
