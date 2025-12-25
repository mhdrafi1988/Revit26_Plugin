using System;
using System.Collections.ObjectModel;
using System.Windows;
using Revit26_Plugin.Creaser_V100.Models;

namespace Revit26_Plugin.Creaser_V100.Services
{
    /// <summary>
    /// UI-safe logging service that pushes log entries
    /// into an ObservableCollection bound to the UI.
    /// </summary>
    public class UiLogService : ILogService
    {
        private readonly ObservableCollection<LogEntry> _logEntries;

        public UiLogService(ObservableCollection<LogEntry> logEntries)
        {
            _logEntries = logEntries
                ?? throw new ArgumentNullException(nameof(logEntries));
        }

        public void Info(string source, string message)
        {
            Add("INFO", source, message);
        }

        public void Warning(string source, string message)
        {
            Add("WARN", source, message);
        }

        public void Error(string source, string message)
        {
            Add("ERROR", source, message);
        }

        public IDisposable Scope(string source, string scopeName)
        {
            return new LogScope(this, source, scopeName);
        }

        private void Add(string level, string source, string message)
        {
            void AddInternal()
            {
                _logEntries.Add(
                    new LogEntry(level, source, message));
            }

            // Ensure UI-thread safety
            if (Application.Current?.Dispatcher?.CheckAccess() == true)
            {
                AddInternal();
            }
            else
            {
                Application.Current?.Dispatcher?.Invoke(AddInternal);
            }
        }

        /// <summary>
        /// Disposable scope for automatic enter/exit logging.
        /// </summary>
        private sealed class LogScope : IDisposable
        {
            private readonly UiLogService _logger;
            private readonly string _source;
            private readonly string _scopeName;
            private bool _disposed;

            public LogScope(
                UiLogService logger,
                string source,
                string scopeName)
            {
                _logger = logger;
                _source = source;
                _scopeName = scopeName;

                _logger.Info(_source, $"ENTER: {_scopeName}");
            }

            public void Dispose()
            {
                if (_disposed) return;

                _logger.Info(_source, $"EXIT: {_scopeName}");
                _disposed = true;
            }
        }
    }
}
