using System;
using System.Collections.Generic;
using System.IO;

namespace Revit26_Plugin.RRLPV4.Utils
{
    /// <summary>
    /// Lightweight logging helper for the Roof Ridge Plugin.
    /// Writes both in-memory + daily rotating log files.
    /// </summary>
    public static class Logger
    {
        private static readonly List<string> _entries = new List<string>();
        private static readonly string _filePath = BuildLogFilePath();

        /// <summary>
        /// Exposes current log entries stored in memory.
        /// </summary>
        public static IReadOnlyList<string> Entries => _entries.AsReadOnly();

        // ----------------------------
        // PUBLIC API
        // ----------------------------

        public static void LogInfo(string message) => Write("INFO", message);

        public static void LogWarning(string message) => Write("WARN", message);

        public static void LogError(string message) => Write("ERROR", message);

        public static void LogException(Exception ex, string context = null)
        {
            string msg = string.IsNullOrEmpty(context)
                ? ex.Message
                : $"{context}: {ex.Message}";

            string log =
                $"[EXCEPTION] {Timestamp()} - {msg}\nStackTrace: {ex.StackTrace}";

            Add(log);
            WriteToDisk(log);
        }

        public static void Clear() => _entries.Clear();

        /// <summary>
        /// Returns all log entries as a single formatted text block.
        /// </summary>
        public static string GetFullLog() => string.Join(Environment.NewLine, _entries);

        // ----------------------------
        // INTERNAL HELPERS
        // ----------------------------

        private static void Write(string tag, string message)
        {
            string entry = $"[{tag}] {Timestamp()} - {message}";
            Add(entry);
            WriteToDisk(entry);
        }

        private static void Add(string entry)
        {
            // Prevent unlimited memory growth (safety)
            if (_entries.Count > 2000)
                _entries.RemoveAt(0);

            _entries.Add(entry);
        }

        private static void WriteToDisk(string entry)
        {
            try
            {
                string dir = Path.GetDirectoryName(_filePath);
                if (!Directory.Exists(dir))
                    Directory.CreateDirectory(dir);

                File.AppendAllText(_filePath, entry + Environment.NewLine);
            }
            catch
            {
                // Silent fail — never break plugin due to logging failure.
            }
        }

        private static string Timestamp() =>
            DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");

        private static string BuildLogFilePath()
        {
            string root = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            string company = "Revit22Plugin";
            string product = "RRLPV3";
            string file = $"RoofPlugin_{DateTime.Now:yyyyMMdd}.log";

            return Path.Combine(root, company, product, "Logs", file);
        }
    }
}
