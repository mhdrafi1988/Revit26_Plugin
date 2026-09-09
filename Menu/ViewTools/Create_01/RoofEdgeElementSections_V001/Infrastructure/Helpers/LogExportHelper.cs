using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Revit26_Plugin.Shared.Models;

namespace Revit26_Plugin.RoofEdgeElementSections.V001
{
    /// <summary>
    /// Writes log entries to a .txt file using the standard naming convention:
    /// RoofEdgeElementSections_Logs_{yyyy-MM-dd}_{HH-mm}.txt
    /// Copied verbatim (naming convention adapted) from RoofEdgeAroundSections_V004.
    /// </summary>
    public static class LogExportHelper
    {
        public static string BuildFileName()
        {
            DateTime now = DateTime.Now;
            return $"RoofEdgeElementSections_Logs_{now:yyyy-MM-dd}_{now:HH-mm}.txt";
        }

        public static void Export(IEnumerable<LogEntry> entries, string folderPath)
        {
            Directory.CreateDirectory(folderPath);
            string fullPath = Path.Combine(folderPath, BuildFileName());
            File.WriteAllLines(fullPath, entries.Select(e => e.ToString()));
        }
    }
}
