using Revit26_Plugin.Shared.Models;
using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text;

namespace Revit26_Plugin.Shared.Services
{
    /// <summary>Writes a tool's log panel to a .txt file at a caller-chosen folder.</summary>
    public static class LogExportService
    {
        /// <summary>Writes the log to {folder}\{toolName}_Logs_{yyyy-MM-dd}_{HH-mm}.txt. Returns the full path written.</summary>
        public static string Export(string toolName, ObservableCollection<LogEntry> log, string folder)
        {
            Directory.CreateDirectory(folder);
            var now = DateTime.Now;
            string fileName = $"{toolName}_Logs_{now:yyyy-MM-dd}_{now:HH-mm}.txt";
            string fullPath = Path.Combine(folder, fileName);
            string text = string.Join(Environment.NewLine, log.Select(l => l.ToString()));
            File.WriteAllText(fullPath, text, Encoding.UTF8);
            return fullPath;
        }
    }
}
