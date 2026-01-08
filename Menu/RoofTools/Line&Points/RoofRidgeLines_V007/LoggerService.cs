using System;
using System.Collections.Generic;
using System.IO;

namespace Revit26_Plugin.RoofTools.LineAndPoints.RoofRidgeLines_V08.Services
{
    public static class LoggerService
    {
        private static readonly List<string> _entries = new List<string>();

        public static void LogInfo(string message) => Write("INFO", message);
        public static void LogWarning(string message) => Write("WARN", message);
        public static void LogError(string message) => Write("ERROR", message);

        public static void LogException(Exception ex, string context = null)
        {
            string msg = string.IsNullOrEmpty(context)
                ? ex.Message
                : $"{context}: {ex.Message}";

            string log = $"[EXCEPTION] {DateTime.Now:yyyy-MM-dd HH:mm:ss} - {msg}\nStackTrace: {ex.StackTrace}";
            Add(log);
        }

        private static void Write(string tag, string message)
        {
            string entry = $"[{tag}] {DateTime.Now:yyyy-MM-dd HH:mm:ss} - {message}";
            Add(entry);
        }

        private static void Add(string entry)
        {
            if (_entries.Count > 2000)
                _entries.RemoveAt(0);
            _entries.Add(entry);
        }

        public static IReadOnlyList<string> GetLogs() => _entries.AsReadOnly();
        public static void ClearLogs() => _entries.Clear();
    }
}