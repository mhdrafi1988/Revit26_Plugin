using System;
using System.Windows.Media;

namespace Revit26_Plugin.CSFL_V07.Models
{
    public class LogEntry
    {
        public DateTime Time { get; } = DateTime.Now;
        public string Message { get; }
        public Brush Color { get; }

        public LogEntry(string message, Brush color)
        {
            Message = message;
            Color = color;
        }

        public override string ToString()
            => $"[{Time:HH:mm:ss}] {Message}";
    }
}
