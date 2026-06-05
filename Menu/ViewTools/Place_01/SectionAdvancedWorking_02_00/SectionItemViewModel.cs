using Autodesk.Revit.DB;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace Revit22_Plugin.SectionPlacer.ViewModels
{
    /// <summary>
    /// Represents a single section view entry in the UI list.
    /// </summary>
    public class SectionItemViewModel : INotifyPropertyChanged
    {
        public ViewSection Section { get; }

        public string Name => Section?.Name ?? "(Unnamed)";
        public int Scale => Section?.Scale ?? 1;

        private bool _isSelected;
        public bool IsSelected
        {
            get => _isSelected;
            set
            {
                if (_isSelected == value) return;
                _isSelected = value;
                OnPropertyChanged();
            }
        }

        public SectionItemViewModel(ViewSection section)
        {
            Section = section;
            IsSelected = true; // Default to selected (user can deselect)
        }

        public event PropertyChangedEventHandler PropertyChanged;
        private void OnPropertyChanged([CallerMemberName] string prop = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(prop));
    }
}
