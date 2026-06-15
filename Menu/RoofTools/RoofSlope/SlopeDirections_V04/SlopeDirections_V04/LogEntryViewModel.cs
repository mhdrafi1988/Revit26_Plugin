using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace Revit_26.CornertoDrainArrow_V05
{
    /// <summary>
    /// Single log row for the UI log panel.
    /// </summary>
    public sealed class LogEntryViewModel : INotifyPropertyChanged
    {
        private string _message;
        private LogEntryLevel _level;

        public string Message
        {
            get => _message;
            set { _message = value; OnPropertyChanged(); }
        }

        public LogEntryLevel Level
        {
            get => _level;
            set { _level = value; OnPropertyChanged(); }
        }

        public event PropertyChangedEventHandler PropertyChanged;

        private void OnPropertyChanged([CallerMemberName] string name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
