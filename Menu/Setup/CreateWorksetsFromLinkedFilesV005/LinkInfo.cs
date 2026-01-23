using System.ComponentModel;

namespace Revit26_Plugin.WSA_V05.Models
{
    /// <summary>
    /// Represents a single Link row in the UI Grid.
    /// </summary>
    public class LinkInfo : INotifyPropertyChanged
    {
        public string LinkName { get; }

        private string _targetWorkset;
        public string TargetWorkset
        {
            get => _targetWorkset;
            set { _targetWorkset = value; OnChanged(nameof(TargetWorkset)); }
        }

        private bool _worksetExists;
        public bool WorksetExists
        {
            get => _worksetExists;
            set { _worksetExists = value; OnChanged(nameof(WorksetExists)); }
        }

        private bool _isSelected;
        public bool IsSelected
        {
            get => _isSelected;
            set { _isSelected = value; OnChanged(nameof(IsSelected)); }
        }

        public LinkInfo(string linkName)
        {
            LinkName = linkName;
        }

        public event PropertyChangedEventHandler PropertyChanged;
        private void OnChanged(string p) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(p));
    }
}