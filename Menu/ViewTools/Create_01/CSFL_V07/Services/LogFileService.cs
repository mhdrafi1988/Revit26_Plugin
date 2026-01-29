using System;
using System.Collections.ObjectModel;
using System.IO;
using Revit26_Plugin.CSFL_V07.Models;

namespace Revit26_Plugin.CSFL_V07.Services.Logging
{
    public class LogFileService
    {
        public string FilePath { get; }

        public LogFileService()
        {
            string dir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                "Revit26_CSFL_Logs");

            Directory.CreateDirectory(dir);

            FilePath = Path.Combine(
                dir, $"CSFL_{DateTime.Now:yyyyMMdd_HHmmss}.log");
        }

        public void Write(ObservableCollection<LogEntry> entries)
        {
            using StreamWriter sw = new(FilePath);
            foreach (var e in entries)
                sw.WriteLine(e.ToString());
        }
    }
}
