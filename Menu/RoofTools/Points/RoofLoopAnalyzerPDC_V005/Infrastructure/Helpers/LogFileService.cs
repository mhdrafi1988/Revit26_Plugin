// LogFileService.cs
// Namespace: Revit26_Plugin.RoofLoopAnalyzerPDC.V005
// Writes the session log to disk. Filename convention (project logging rule):
//     {ToolName}_Logs_{yyyy-MM-dd}_{HH-mm}.txt
// The folder is resolved once per session and reused.

using Revit26_Plugin.Shared.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Revit26_Plugin.RoofLoopAnalyzerPDC.V005.Infrastructure.Helpers
{
    public static class LogFileService
    {
        private const string ToolName = "RoofLoopAnalyzerPDC";

        private static string _sessionFolder;

        public static string ResolveLogFolder(string preferred, Action<string> warn = null)
        {
            if (!string.IsNullOrEmpty(_sessionFolder) && Directory.Exists(_sessionFolder))
                return _sessionFolder;

            var candidate = !string.IsNullOrWhiteSpace(preferred)
                ? preferred
                : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                               "Revit26_Plugin_Logs");
            try
            {
                Directory.CreateDirectory(candidate);
                _sessionFolder = candidate;
                return _sessionFolder;
            }
            catch (Exception ex)
            {
                warn?.Invoke($"Could not create log folder '{candidate}' ({ex.Message}).");
                return null;
            }
        }

        /// <summary>Returns the written path, or null when the write failed.</summary>
        public static string Write(string folder, IEnumerable<LogEntry> entries, Action<string> error = null)
        {
            if (string.IsNullOrEmpty(folder) || entries == null) return null;
            try
            {
                var name = $"{ToolName}_Logs_{DateTime.Now:yyyy-MM-dd}_{DateTime.Now:HH-mm}.txt";
                var path = Path.Combine(folder, name);
                File.WriteAllLines(path, entries.Select(e => e.ToString()));
                return path;
            }
            catch (Exception ex)
            {
                error?.Invoke($"Could not write log file ({ex.GetType().Name}: {ex.Message}).");
                return null;
            }
        }
    }
}
