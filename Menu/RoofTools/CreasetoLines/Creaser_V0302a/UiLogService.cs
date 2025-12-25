using System.Collections.ObjectModel;

namespace Revit26_Plugin.Creaser_V03_03.Helpers
{
    public class UiLogService
    {
        public ObservableCollection<string> Messages { get; } = new();
        public void Log(string msg) => Messages.Add(msg);
    }
}
