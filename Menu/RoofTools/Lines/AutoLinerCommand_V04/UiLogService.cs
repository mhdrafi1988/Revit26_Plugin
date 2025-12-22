using System.Collections.ObjectModel;
using Revit26_Plugin.AutoLiner_V04.Models;

namespace Revit26_Plugin.AutoLiner_V04.Services
{
    public class UiLogService
    {
        public ObservableCollection<UiLogEntry> Entries { get; }
            = new ObservableCollection<UiLogEntry>();

        public void Info(string message)
        {
            Entries.Add(new UiLogEntry(message));
        }
    }
}
