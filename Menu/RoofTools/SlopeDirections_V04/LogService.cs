using System;
using System.Collections.ObjectModel;
using System.Windows.Threading;

namespace Revit_26.CornertoDrainArrow_V05
{
    public enum LogEntryLevel { Info, Warning, Error, Debug }

    public interface ILogService
    {
        ObservableCollection<LogEntryViewModel> Entries { get; }
        void Info(string message);
        void Error(string message);
        void Log(string message, LogEntryLevel level = LogEntryLevel.Info);
        void Log(Exception ex, LogEntryLevel level = LogEntryLevel.Error);
        event EventHandler<LogEntryViewModel> LogEntryAdded;
    }

    public sealed class LogService : ILogService
    {
        private readonly Dispatcher _dispatcher;
        public ObservableCollection<LogEntryViewModel> Entries { get; } = new();
        public event EventHandler<LogEntryViewModel> LogEntryAdded;

        public LogService()
        {
            _dispatcher = Dispatcher.CurrentDispatcher;
        }

        public void Info(string message) => Add(message, LogEntryLevel.Info);
        public void Error(string message) => Add(message, LogEntryLevel.Error);

        public void Log(string message, LogEntryLevel level = LogEntryLevel.Info)
        {
            var entry = new LogEntryViewModel
            {
                Message = $"[{DateTime.Now:HH:mm:ss}] {message}",
                Level = level
            };
            Add(entry);
        }

        public void Log(Exception ex, LogEntryLevel level = LogEntryLevel.Error)
        {
            Log($"{ex.GetType().Name}: {ex.Message}\n{ex.StackTrace}", level);
        }

        private void Add(string message, LogEntryLevel level)
        {
            var entry = new LogEntryViewModel { Message = message, Level = level };
            Add(entry);
        }

        private void Add(LogEntryViewModel entry)
        {
            _dispatcher.Invoke(() =>
            {
                Entries.Add(entry);
                LogEntryAdded?.Invoke(this, entry);
            });
        }
    }
}