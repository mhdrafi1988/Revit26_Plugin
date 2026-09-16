using Revit26_Plugin.Shared.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Revit26_Plugin.ParaManager.V001.Services
{
    /// <summary>
    /// Writes the ObservableCollection&lt;LogEntry&gt; log to a .txt file.
    /// Filename convention: {ToolName}_Logs_{yyyy-MM-dd}_{HH-mm}.txt (suite standard).
    /// </summary>
    public static class LogExportService
    {
        private const string ToolName = "ParaManager";

        public static string BuildFileName(DateTime timestamp)
            => $"{ToolName}_Logs_{timestamp:yyyy-MM-dd}_{timestamp:HH-mm}.txt";

        /// <summary>Writes all log entries to the given folder, returns the full file path written.</summary>
        public static string Export(IEnumerable<LogEntry> entries, string folder)
        {
            Directory.CreateDirectory(folder);
            var fileName = BuildFileName(DateTime.Now);
            var fullPath = Path.Combine(folder, fileName);

            File.WriteAllLines(fullPath, entries.Select(e => e.ToString()));
            return fullPath;
        }
    }
}
