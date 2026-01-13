using Autodesk.Revit.DB;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace Revit26_Plugin.CalloutCOP_V04.Models
{
    public class CalloutItem : INotifyPropertyChanged
    {
        public ElementId ViewId { get; init; }
        public ElementId SheetId { get; init; }

        public string SectionName { get; init; }
        public string SheetNumber { get; init; }
        public string DetailNumber { get; init; }

        public bool IsPlaced { get; init; }

        private bool _isSelected;
        public bool IsSelected
        {
            get => _isSelected;
            set { _isSelected = value; OnPropertyChanged(); }
        }

        public string Status => IsPlaced ? $"Placed ({SheetNumber})" : "Unplaced";

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string n = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));
    }
}
