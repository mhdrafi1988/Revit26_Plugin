using System.Collections.ObjectModel;
using System.Windows;
using Revit26_Plugin.Shared.Models;

namespace Revit26_Plugin.CreateSectionsFromDetailLines.V008.Services
{
    /// <summary>
    /// V008: migrated to the shared Revit26_Plugin.Shared.Models.LogEntry/LogLevel
    /// instead of V07's tool-local LogEntry (which incorrectly baked a WPF
    /// Brush directly into the model). Color now comes from the shared
    /// LogLevelToColorConverter in XAML, per suite convention.
    /// Added Success(...) to support "Created X" messages distinctly from Info.
    /// </summary>
    public class LiveLogService
    {
        private readonly ObservableCollection<LogEntry> _log;

        public LiveLogService(ObservableCollection<LogEntry> log)
        {
            _log = log;
        }

        private void Add(LogLevel level, string msg)
        {
            Application.Current.Dispatcher.Invoke(() =>
                _log.Add(new LogEntry(level, msg)));
        }

        public void Info(string msg) => Add(LogLevel.Info, msg);
        public void Warn(string msg) => Add(LogLevel.Warning, msg);
        public void Error(string msg) => Add(LogLevel.Error, msg);
        public void Success(string msg) => Add(LogLevel.Success, msg);
    }
}
