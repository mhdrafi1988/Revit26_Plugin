using System;

namespace Revit26_Plugin.AutoSlope.V5_00.Infrastructure.Helpers
{
    public static class LogColorHelper
    {
        private static string Stamp => DateTime.Now.ToString("HH:mm:ss");

        private static string Wrap(string color, string msg)
        {
            return $"<color={color}>[{Stamp}] {msg}</color>";
        }

        public static string Green(string message) => Wrap("#2ECC71", message);
        public static string Yellow(string message) => Wrap("#F1C40F", message);
        public static string Red(string message) => Wrap("#E74C3C", message);
        public static string Cyan(string message) => Wrap("#1ABC9C", message);
        public static string Blue(string message) => Wrap("#3498DB", message);
        public static string Orange(string message) => Wrap("#E67E22", message);
    }
}