using System;

namespace Revit26_Plugin.AutoSlopeByPoint_30_07.Helpers
{
    public static class LogColorHelper
    {
        private static string Stamp => DateTime.Now.ToString("HH:mm:ss");

        private static string Wrap(string color, string msg)
            => $"<color={color}>[{Stamp}] {msg}</color>";

        public static string Green(string m) => Wrap("#2ECC71", m); // success
        public static string Yellow(string m) => Wrap("#F1C40F", m); // warning
        public static string Red(string m) => Wrap("#E74C3C", m); // error
        public static string Balck(string m) => Wrap("#1ABC9C", m); // info
        public static string Cyan(string m) => Wrap("#1ABC9C", m); // info
    }
}
