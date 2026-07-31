using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Revit26_Plugin.Shared.Models;

namespace Revit26_Plugin.Shared.Services
{
    /// <summary>
    /// Writes a tool's log panel contents to a timestamped .txt file.
    /// Filename pattern: {toolName}_Logs_{yyyy-MM-dd}_{HH-mm}.txt
    ///
    /// Generalized from DtlLineDim V006's Infrastructure/Helpers/LogExportService.cs
    /// during the V007 shared-infra refactor. Each tool passes its own short name
    /// as the filename prefix.
    ///
    /// Usage:
    ///   LogExportService.Export("DtlLineDim", LogEntries, folderPath);
    /// </summary>
    public static class LogExportService
    {
        public static string BuildFileName(string toolName)
        {
            var now = DateTime.Now;
            return $"{toolName}_Logs_{now:yyyy-MM-dd}_{now:HH-mm}.txt";
        }

        public static void Export(string toolName, IEnumerable<LogEntry> entries, string folderPath)
        {
            if (string.IsNullOrWhiteSpace(folderPath))
                throw new ArgumentException("Export folder path is empty.");

            if (!Directory.Exists(folderPath))
                Directory.CreateDirectory(folderPath);

            string fullPath = Path.Combine(folderPath, BuildFileName(toolName));
            string content = string.Join(Environment.NewLine, entries.Select(e => e.ToString()));
            File.WriteAllText(fullPath, content);
        }
    }
}
