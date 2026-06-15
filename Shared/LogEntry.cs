using Revit26_Plugin.Shared.Models;
using System;

namespace Revit26_Plugin.WorksetManager_06.Models
{
    public class LogEntry
    {
        public DateTime Time    { get; }
        public LogLevel  Level   { get; }
        public string    Message { get; }

        public LogEntry(LogLevel level, string message)
        {
            Time    = DateTime.Now;
            Level   = level;
            Message = message;
        }

        // Used by the Copy Log feature — produces one line per entry
        public override string ToString()
            => $"{Time:HH:mm:ss}  {Level,-7}  {Message}";
    }
}
