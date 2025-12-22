using System;

namespace Revit26_Plugin.AutoLiner_V04.Models
{
    public class UiLogEntry
    {
        public DateTime Time { get; } = DateTime.Now;
        public string Message { get; }

        public UiLogEntry(string message)
        {
            Message = message;
        }

        public override string ToString()
        {
            return $"[{Time:HH:mm:ss}] {Message}";
        }
    }
}
