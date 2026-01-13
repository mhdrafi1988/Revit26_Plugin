using System.Collections.ObjectModel;

namespace Revit22_Plugin.copv3.Services
{
    public class CalloutCOPV3LoggerService
    {
        public ObservableCollection<string> LogItems { get; } = new ObservableCollection<string>();

        public void Write(string message)
        {
            LogItems.Add(message);
        }
    }
}
