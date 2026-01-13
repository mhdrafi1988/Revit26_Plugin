using Autodesk.Revit.DB;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace Revit22_Plugin.copv2.Models
{
    public class CalloutViewModelCall : INotifyPropertyChanged
    {
        private string _sectionName;
        private string _sheetName;
        private string _sheetNumber;
        private ElementId _sheetId;
        private string _detailNumber;
        private ElementId _viewId;
        private bool _isPlaced;
        private bool _isSelected;
        private bool _isVisible = true;

        public string SectionName
        {
            get => _sectionName;
            set
            {
                if (_sectionName == value) return;
                _sectionName = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(DisplayInfo));
            }
        }

        public string SheetName
        {
            get => _sheetName;
            set
            {
                if (_sheetName == value) return;
                _sheetName = value;
                OnPropertyChanged();
            }
        }

        public string SheetNumber
        {
            get => _sheetNumber;
            set
            {
                if (_sheetNumber == value) return;
                _sheetNumber = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(DisplayInfo));
            }
        }

        public ElementId SheetId
        {
            get => _sheetId;
            set
            {
                if (_sheetId == value) return;
                _sheetId = value;
                OnPropertyChanged();
            }
        }

        public string DetailNumber
        {
            get => _detailNumber;
            set
            {
                if (_detailNumber == value) return;
                _detailNumber = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(DisplayInfo));
            }
        }

        public ElementId ViewId
        {
            get => _viewId;
            set
            {
                if (_viewId == value) return;
                _viewId = value;
                OnPropertyChanged();
            }
        }

        public bool IsPlaced
        {
            get => _isPlaced;
            set
            {
                if (_isPlaced == value) return;
                _isPlaced = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(DisplayInfo));
            }
        }

        // User selection (checkbox)
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

        // Used for filtering (Sheet Filter, Search Filter)
        public bool IsVisible
        {
            get => _isVisible;
            set
            {
                if (_isVisible == value) return;
                _isVisible = value;
                OnPropertyChanged();
            }
        }

        // Convenient display text for compact UI row
        public string DisplayInfo
        {
            get
            {
                if (IsPlaced)
                {
                    string det = string.IsNullOrWhiteSpace(DetailNumber) ? "-" : DetailNumber;
                    return $"{SheetNumber} / {det}";
                }
                return "Unplaced";
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;

        protected void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
