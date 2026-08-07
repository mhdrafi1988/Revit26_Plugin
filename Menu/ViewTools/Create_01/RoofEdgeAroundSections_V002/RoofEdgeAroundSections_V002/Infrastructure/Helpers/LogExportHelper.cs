using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Revit26_Plugin.Shared.Models;

namespace Revit26_Plugin.RoofEdgeSections.V002
{
    /// <summary>
    /// Writes log entries to a .txt file using the standard naming convention:
    /// RoofEdgeSections_Logs_{yyyy-MM-dd}_{HH-mm}.txt
    /// </summary>
    public static class LogExportHelper
    {
        public static string BuildFileName()
        {
            DateTime now = DateTime.Now;
            return $"RoofEdgeSections_Logs_{now:yyyy-MM-dd}_{now:HH-mm}.txt";
        }

        public static void Export(IEnumerable<LogEntry> entries, string folderPath)
        {
            Directory.CreateDirectory(folderPath);
            string fullPath = Path.Combine(folderPath, BuildFileName());
            File.WriteAllLines(fullPath, entries.Select(e => e.ToString()));
        }
    }
}
