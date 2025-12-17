using System;

namespace Revit22_Plugin.AutoSlopeV3.Helpers
{
    public static class LogColorHelper
    {
        private static string Stamp => DateTime.Now.ToString("HH:mm:ss");

        private static string Wrap(string color, string text)
        {
            return
                $"<Span xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation'>" +
                $"<Run Foreground='{color}'>[{Stamp}] {text}</Run>" +
                $"</Span>";
        }

        public static string Black(string msg) => Wrap("Black", msg);
        public static string Green(string msg) => Wrap("Green", msg);
        public static string Yellow(string msg) => Wrap("Goldenrod", msg);
        public static string Red(string msg) => Wrap("Red", msg);
        public static string Cyan(string msg) => Wrap("DarkCyan", msg);
    }
}
