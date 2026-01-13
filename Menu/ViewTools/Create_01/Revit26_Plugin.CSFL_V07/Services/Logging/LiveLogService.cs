using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Media;
using Revit26_Plugin.CSFL_V07.Models;

namespace Revit26_Plugin.CSFL_V07.Services.Logging
{
    public class LiveLogService
    {
        private readonly ObservableCollection<LogEntry> _log;

        public LiveLogService(ObservableCollection<LogEntry> log)
        {
            _log = log;
        }

        private void Add(string msg, Brush color)
        {
            Application.Current.Dispatcher.Invoke(() =>
                _log.Add(new LogEntry(msg, color)));
        }

        public void Info(string msg) => Add(msg, Brushes.LightGray);
        public void Warn(string msg) => Add(msg, Brushes.Gold);
        public void Error(string msg) => Add(msg, Brushes.OrangeRed);
    }
}
