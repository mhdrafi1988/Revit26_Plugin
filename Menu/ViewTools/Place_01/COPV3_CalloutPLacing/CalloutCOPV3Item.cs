using Autodesk.Revit.DB;
using System.ComponentModel;

namespace Revit22_Plugin.copv3.Models
{
    public class CalloutCOPV3Item : INotifyPropertyChanged
    {
        public string SectionName { get; set; }
        public string SheetNumber { get; set; }
        public string DetailNumber { get; set; }

        public bool IsPlaced { get; set; }
        public ElementId ViewId { get; set; }
        public ElementId SheetId { get; set; }

        private bool _isSelected;
        public bool IsSelected
        {
            get => _isSelected;
            set { _isSelected = value; OnPropertyChanged(nameof(IsSelected)); }
        }

        public string DisplayStatus =>
            IsPlaced ? $"Placed ({SheetNumber})" : "Unplaced";

        public event PropertyChangedEventHandler PropertyChanged;
        private void OnPropertyChanged(string prop) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(prop));
    }
}
