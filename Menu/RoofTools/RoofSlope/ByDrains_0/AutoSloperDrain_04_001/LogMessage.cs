using System.Windows.Media;

namespace Revit22_Plugin.Asd_V4_01.Models
{
    public class LogMessage
    {
        public string Message { get; set; }
        public SolidColorBrush Color { get; set; }
        public string Timestamp { get; set; }

        public LogMessage(string msg, SolidColorBrush color)
        {
            Message = msg;
            Color = color;
            Timestamp = System.DateTime.Now.ToString("HH:mm:ss");
        }

        public override string ToString()
        {
            return $"[{Timestamp}] {Message}";
        }
    }
}
