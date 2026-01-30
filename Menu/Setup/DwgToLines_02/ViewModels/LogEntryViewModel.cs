using System.Windows.Media;

namespace Revit26_Plugin.DwgSymbolicConverter_V02.ViewModels
{
    /// <summary>
    /// Single log entry displayed in the live UI log panel.
    /// </summary>
    public class LogEntryViewModel
    {
        public string Message { get; }
        public Brush LevelColor { get; }

        public LogEntryViewModel(string message, Brush levelColor)
        {
            Message = message;
            LevelColor = levelColor;
        }
    }
}
