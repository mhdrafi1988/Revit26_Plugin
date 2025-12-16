using System.Collections.ObjectModel;

namespace Revit26_Plugin.AutoLiner_V01.Services
{
    public static class LogService
    {
        public static void LogTop(
            ObservableCollection<string> logs,
            string message)
        {
            logs.Insert(0, message);
        }

        public static void LogBottom(
            ObservableCollection<string> logs,
            string message)
        {
            logs.Add(message);
        }
    }
}
