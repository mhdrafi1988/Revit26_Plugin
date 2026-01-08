// File: UILogSink.cs
// Namespace: Revit26_Plugin.RoofRidgeLines_V06.Logging
//
// Responsibility:
// - Central sink for UI-visible log entries
// - Thread-safe dispatch to UI-bound collections
// - Can be wired to Serilog later

using System;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Threading;

namespace Revit26_Plugin.RoofRidgeLines_V06.Logging
{
    /// <summary>
    /// Central UI log sink used by services and execution engine.
    /// </summary>
    public class UILogSink
    {
        private readonly ObservableCollection<UILogEntry> _logEntries;
        private readonly Dispatcher _dispatcher;

        public ObservableCollection<UILogEntry> LogEntries => _logEntries;

        public UILogSink(ObservableCollection<UILogEntry> logEntries)
        {
            _logEntries = logEntries ?? throw new ArgumentNullException(nameof(logEntries));

            // Capture UI dispatcher safely
            _dispatcher = Application.Current?.Dispatcher ?? Dispatcher.CurrentDispatcher;
        }

        /// <summary>
        /// Adds a log entry to the UI in a thread-safe manner.
        /// </summary>
        public void Log(LogCategory category, string message)
        {
            if (string.IsNullOrWhiteSpace(message))
                return;

            void AddEntry()
            {
                _logEntries.Add(new UILogEntry(category, message));
            }

            if (_dispatcher.CheckAccess())
            {
                AddEntry();
            }
            else
            {
                _dispatcher.BeginInvoke((Action)AddEntry);
            }
        }
    }
}
