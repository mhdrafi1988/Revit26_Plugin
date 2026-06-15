// ==============================================
// File: LogEntryViewModel.cs
// Layer: ViewModels
// ==============================================

using System.Windows.Media;

namespace Revit26_Plugin.DwgSymbolicConverter_V03.ViewModels
{
    /// <summary>
    /// Single log entry shown in the UI log panel.
    /// </summary>
    public sealed class LogEntryViewModel
    {
        public string Message { get; }
        public Brush Color { get; }

        public LogEntryViewModel(string message, Brush color)
        {
            Message = message;
            Color = color;
        }
    }
}
