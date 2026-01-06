using System;
using System.Collections.ObjectModel;
using System.Windows.Media;

namespace Revit22_Plugin.V4_02.Infrastructure.Logging
{
    /// <summary>
    /// Adapts domain logging calls into UI-friendly log entries.
    /// </summary>
    public class UiLoggerAdapter : IAutoSlopeLogger
    {
        public ObservableCollection<LogMessage> Logs { get; }
            = new ObservableCollection<LogMessage>();

        public void Info(string message)
            => Add(message, Brushes.Black);

        public void Warn(string message)
            => Add(message, Brushes.Goldenrod);

        public void Error(string message)
            => Add(message, Brushes.Red);

        private void Add(string message, SolidColorBrush color)
        {
            Logs.Add(new LogMessage(message, color));
        }
    }
}
