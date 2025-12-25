using System.Collections.ObjectModel;

namespace Revit26_Plugin.Creaser_V03_03.Helpers
{
    /// <summary>
    /// Simple MVVM-safe UI logger for Creaser only
    /// </summary>
    public sealed class UiLogService
    {
        public ObservableCollection<string> Messages { get; } = new();

        public void Log(string message)
        {
            Messages.Add(message);
        }
    }
}
