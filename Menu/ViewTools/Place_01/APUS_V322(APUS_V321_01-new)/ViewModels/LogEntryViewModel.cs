// File: LogEntryViewModel.cs
using System;
using Revit26_Plugin.Shared.Models;

namespace Revit26_Plugin.APUS.V322.ViewModels
{
    /// <summary>
    /// Uses the shared Revit26_Plugin.Shared.Models.LogLevel rather than a
    /// local duplicate (V320 had its own Models.LogLevel enum, which violated
    /// the suite convention of never duplicating shared LogEntry/LogLevel).
    /// </summary>
    public class LogEntryViewModel : BaseViewModel
    {
        private DateTime _timestamp;
        public DateTime Timestamp
        {
            get => _timestamp;
            set => SetField(ref _timestamp, value);
        }

        private LogLevel _level;
        public LogLevel Level
        {
            get => _level;
            set => SetField(ref _level, value);
        }

        private string _message;
        public string Message
        {
            get => _message;
            set => SetField(ref _message, value);
        }

        public string DisplayText => $"[{Timestamp:HH:mm:ss}] {Message}";

        public LogEntryViewModel(LogLevel level, string message)
        {
            Timestamp = DateTime.Now;
            Level = level;
            Message = message ?? string.Empty;
        }

        public LogEntryViewModel(DateTime timestamp, LogLevel level, string message)
        {
            Timestamp = timestamp;
            Level = level;
            Message = message ?? string.Empty;
        }
    }
}
