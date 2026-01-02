using System;
using System.Collections.ObjectModel;

namespace Revit26_Plugin.Creaser_adv_V001.Services
{
    /// <summary>
    /// Centralized logging service for UI and debugging.
    /// </summary>
    public class LoggingService
    {
        public ObservableCollection<string> Messages { get; }
            = new ObservableCollection<string>();

        public void Log(string message)
        {
            string timestamp =
                DateTime.Now.ToString("HH:mm:ss");

            Messages.Add($"[{timestamp}] {message}");
        }

        public void Clear()
        {
            Messages.Clear();
        }
    }
}
