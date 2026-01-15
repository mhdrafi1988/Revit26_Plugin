using System.Collections.ObjectModel;
using Revit26_Plugin.CreaserAdv_V003.Models;

namespace Revit26_Plugin.CreaserAdv_V003.Services
{
    public class LoggingService
    {
        public ObservableCollection<LogEntry> Entries { get; } =
            new ObservableCollection<LogEntry>();

        public void Info(string msg)
        {
            Entries.Add(new LogEntry(msg));
        }
    }
}
