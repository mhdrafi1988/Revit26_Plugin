using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Threading;

namespace Revit26_Plugin.CreaserAdv_V00.Services.Logging
{
    public sealed class LoggingService
    {
        private readonly SynchronizationContext _uiContext;
        private readonly string _filePath;

        public ObservableCollection<LogEntry> Entries { get; }
            = new();

        public LoggingService(string toolName)
        {
            _uiContext = SynchronizationContext.Current
                ?? throw new InvalidOperationException("Must be on UI thread");

            string dir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                "Revit26_Logs",
                toolName);

            Directory.CreateDirectory(dir);
            _filePath = Path.Combine(dir, $"Log_{DateTime.Now:yyyyMMdd_HHmmss}.txt");
        }

        public void Info(string m) => Log(LoggingLevel.Info, m);
        public void Warning(string m) => Log(LoggingLevel.Warning, m);
        public void Error(string m) => Log(LoggingLevel.Error, m);

        public void Clear()
        {
            _uiContext.Post(_ => Entries.Clear(), null);
        }

        private void Log(LoggingLevel level, string message)
        {
            var entry = new LogEntry(level, message);

            _uiContext.Post(_ => Entries.Add(entry), null);
            File.AppendAllText(_filePath, entry + Environment.NewLine);
        }
    }
}
