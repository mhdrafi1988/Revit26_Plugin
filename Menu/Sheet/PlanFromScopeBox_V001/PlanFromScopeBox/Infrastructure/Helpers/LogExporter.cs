using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Revit26_Plugin.Shared.Models;

namespace Revit26_Plugin.PlanFromScopeBox.V001.Infrastructure.Helpers
{
    /// <summary>
    /// Writes activity-log entries to a .txt file. Used for both silent auto-save on
    /// completion (H3) and the manual Export button. Filename format:
    /// PlanFromScopeBox_Logs_{yyyy-MM-dd}_{HH-mm}.txt
    /// </summary>
    public static class LogExporter
    {
        public static string BuildFileName()
            => $"PlanFromScopeBox_Logs_{DateTime.Now:yyyy-MM-dd}_{DateTime.Now:HH-mm}.txt";

        /// <summary>Writes entries to the given directory, returning the full path written, or
        /// null on failure. Never throws.</summary>
        public static string Write(IEnumerable<LogEntry> entries, string directory)
        {
            try
            {
                Directory.CreateDirectory(directory);
                string path = Path.Combine(directory, BuildFileName());
                var sb = new StringBuilder();
                foreach (LogEntry e in entries) sb.AppendLine(e.ToString());
                File.WriteAllText(path, sb.ToString());
                return path;
            }
            catch
            {
                return null;
            }
        }

        /// <summary>Writes entries to an explicit full file path (for manual Save-As Export).
        /// Returns true on success.</summary>
        public static bool WriteTo(IEnumerable<LogEntry> entries, string fullPath)
        {
            try
            {
                var sb = new StringBuilder();
                foreach (LogEntry e in entries) sb.AppendLine(e.ToString());
                File.WriteAllText(fullPath, sb.ToString());
                return true;
            }
            catch
            {
                return false;
            }
        }
    }
}
