using System;

namespace Revit26_Plugin.CreaserAdv_V003.Models
{
    public class LogEntry
    {
        public DateTime Time { get; }
        public string Message { get; }

        public LogEntry(string message)
        {
            Time = DateTime.Now;
            Message = message;
        }
    }
}
