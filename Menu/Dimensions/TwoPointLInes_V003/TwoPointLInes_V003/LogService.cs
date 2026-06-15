using System.Collections.ObjectModel;

namespace Revit26_Plugin.DtlLineDim_V03.Services
{
    public static class LogService
    {
        public static void Log(ObservableCollection<string> log, string message)
        {
            log.Insert(0, message);
        }
    }
}
