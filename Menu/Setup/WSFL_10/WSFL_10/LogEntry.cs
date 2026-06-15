using System;

namespace Revit26_Plugin.WSFL_010.Models
{
    public enum LogLevel
    {
        Info,
        Warning,
        Error
    }

    public class LogEntry
    {
        public LogLevel Level     { get; }
        public string   Message   { get; }

        /// <summary>
        /// Exposed as "Time" because the XAML binds to {Binding Time, StringFormat=HH:mm:ss}.
        /// </summary>
        public DateTime Time      { get; }

        public LogEntry(LogLevel level, string message)
        {
            Level   = level;
            Message = message;
            Time    = DateTime.Now;
        }
    }
}
