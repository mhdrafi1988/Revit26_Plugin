using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Threading;
using System.Windows;

namespace Revit26_Plugin.CreaserAdv_V002.Services
{
    /// <summary>
    /// Centralized logging service.
    /// Streams logs to UI and writes to disk.
    /// </summary>
    public sealed class LoggingService
    {
        private readonly string _logFilePath;
        private readonly SynchronizationContext _uiContext;

        public ObservableCollection<LogEntry> Entries { get; }
            = new ObservableCollection<LogEntry>();

        public LoggingService(string toolName)
        {
            _uiContext = SynchronizationContext.Current
                ?? throw new InvalidOperationException(
                    "LoggingService must be created on UI thread.");

            string folder =
                Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                    "Revit26_Logs",
                    toolName);

            Directory.CreateDirectory(folder);

            _logFilePath =
                Path.Combine(
                    folder,
                    $"Log_{DateTime.Now:yyyyMMdd_HHmmss}.txt");

            WriteFileLine("=== Log Started ===");
        }

        // -----------------------------
        // Public API
        // -----------------------------

        public void Info(string message)
            => Log(LoggingLevel.Info, message);

        public void Warning(string message)
            => Log(LoggingLevel.Warning, message);

        public void Error(string message)
            => Log(LoggingLevel.Error, message);

        public void Clear()
        {
            _uiContext.Post(_ => Entries.Clear(), null);
            WriteFileLine("=== Log Cleared ===");
        }

        // -----------------------------
        // Core logging logic
        // -----------------------------

        private void Log(LoggingLevel level, string message)
        {
            var entry = new LogEntry(level, message);

            // UI-safe update
            _uiContext.Post(_ =>
            {
                Entries.Add(entry);
            }, null);

            // File write (no UI dependency)
            WriteFileLine(entry.ToString());
        }

        private void WriteFileLine(string line)
        {
            try
            {
                File.AppendAllText(
                    _logFilePath,
                    line + Environment.NewLine);
            }
            catch
            {
                // Never throw from logging
            }
        }
    }
}
