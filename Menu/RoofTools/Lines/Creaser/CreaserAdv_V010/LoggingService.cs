// ==================================
// File: LoggingService.cs
// Namespace: Revit26_Plugin.CreaserAdv.V010.Services
// ==================================

using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Threading;
using Revit26_Plugin.Shared.Models;

namespace Revit26_Plugin.CreaserAdv.V010.Services
{
    /// <summary>
    /// Thread-safe logging service.
    /// Streams entries to an <see cref="ObservableCollection{T}"/> bound to the
    /// UI ListBox and simultaneously persists them to a timestamped text file
    /// under <c>%USERPROFILE%\Documents\Revit26_Logs\</c>.
    /// Must be created on the WPF UI thread.
    /// </summary>
    public sealed class LoggingService
    {
        private readonly string                 _logFilePath;
        private readonly SynchronizationContext _uiContext;
        private readonly object                 _fileLock = new object();

        public ObservableCollection<LogEntry> Entries { get; }
            = new ObservableCollection<LogEntry>();

        /// <summary>Full path of the text file this session is writing to.</summary>
        public string LogFilePath => _logFilePath;

        public LoggingService(string toolName)
        {
            _uiContext = SynchronizationContext.Current
                ?? throw new InvalidOperationException(
                    "LoggingService must be created on the UI thread.");

            string folder = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                "Revit26_Logs",
                toolName);

            Directory.CreateDirectory(folder);

            _logFilePath = Path.Combine(
                folder,
                $"Log_{DateTime.Now:yyyyMMdd_HHmmss}.txt");

            WriteFileLine("=== Log Started ===");
        }

        // --------------------------------------------------
        // Public API
        // --------------------------------------------------

        public void Info(string message)    => Log(LogLevel.Info,    message);
        public void Warning(string message) => Log(LogLevel.Warning, message);
        public void Error(string message)   => Log(LogLevel.Error,   message);
        public void Success(string message) => Log(LogLevel.Success, message);

        /// <summary>Verbose per-item detail (individual curves, nodes, lines).</summary>
        public void Debug(string message)   => Log(LogLevel.Debug,   message);

        /// <summary>Visual separator that opens a pipeline step, e.g. "Step 2/7 — Boundary curves".</summary>
        public void Section(string title)   => Log(LogLevel.Info,    $"── {title} ──");

        public void Clear()
        {
            _uiContext.Post(_ => Entries.Clear(), null);
            WriteFileLine("=== Log Cleared ===");
        }

        // --------------------------------------------------
        // Core
        // --------------------------------------------------

        private void Log(LogLevel level, string message)
        {
            var entry = new LogEntry(level, message);
            _uiContext.Post(_ => Entries.Add(entry), null);
            WriteFileLine(entry.ToString());
        }

        private void WriteFileLine(string line)
        {
            try
            {
                lock (_fileLock)
                    File.AppendAllText(_logFilePath, line + Environment.NewLine);
            }
            catch { }
        }
    }
}
