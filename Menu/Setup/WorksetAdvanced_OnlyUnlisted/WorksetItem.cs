using System.ComponentModel;

namespace Revit26_Plugin.WSAV03.Models
{
    public class WorksetItem : INotifyPropertyChanged
    {
        public string LinkName { get; }

        private string _preview;
        public string PreviewName
        {
            get => _preview;
            set { _preview = value; OnChanged(nameof(PreviewName)); }
        }

        private bool _isSelected;
        public bool IsSelected
        {
            get => _isSelected;
            set { _isSelected = value; OnChanged(nameof(IsSelected)); }
        }

        // New property to track assignment status
        private bool _isAssigned;
        public bool IsAssigned
        {
            get => _isAssigned;
            set { _isAssigned = value; OnChanged(nameof(IsAssigned)); }
        }

        public WorksetItem(string linkName)
        {
            LinkName = linkName;
            IsAssigned = false; // Default to not assigned
        }

        public event PropertyChangedEventHandler PropertyChanged;
        private void OnChanged(string p) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(p));
    }
}