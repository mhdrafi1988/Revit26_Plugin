using System;
using System.Windows.Media;

namespace Revit26_Plugin.V5_00.Infrastructure.Logging
{
    public class LogMessage
    {
        public string Message { get; }
        public SolidColorBrush Color { get; }
        public string Timestamp { get; }

        public LogMessage(string message, SolidColorBrush color)
        {
            Message = message;
            Color = color;
            Timestamp = DateTime.Now.ToString("HH:mm:ss");
        }

        public override string ToString()
        {
            return $"[{Timestamp}] {Message}";
        }
    }
}
