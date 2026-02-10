// File: LogEntryViewModel.cs
using Revit26_Plugin.SectionManager_V07.ViewModels;
using System;

namespace Revit26_Plugin.APUS_V314.ViewModels
{
    public enum LogLevel
    {
        Info,
        Warning,
        Error,
        Success
    }

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

        // For timestamp-based constructor (if needed)
        public LogEntryViewModel(DateTime timestamp, LogLevel level, string message)
        {
            Timestamp = timestamp;
            Level = level;
            Message = message ?? string.Empty;
        }
    }
}