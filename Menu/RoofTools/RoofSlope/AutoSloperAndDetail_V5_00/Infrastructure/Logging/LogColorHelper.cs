using System;

namespace Revit26_Plugin.V5_00.Infrastructure.Logging
{
    public static class LogColorHelper
    {
        private static string Stamp =>
            DateTime.Now.ToString("HH:mm:ss");

        private static string Wrap(string color, string text)
        {
            return
                $"<Span xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation'>" +
                $"<Run Foreground='{color}'>[{Stamp}] {text}</Run>" +
                $"</Span>";
        }

        public static string Info(string msg) => Wrap("Black", msg);
        public static string Success(string msg) => Wrap("Green", msg);
        public static string Warning(string msg) => Wrap("Goldenrod", msg);
        public static string Error(string msg) => Wrap("Red", msg);
        public static string Debug(string msg) => Wrap("DarkCyan", msg);
    }
}
