using Autodesk.Revit.DB;
using CommunityToolkit.Mvvm.ComponentModel;
using System;

namespace Revit26_Plugin.APUS_V314.ViewModels
{
    public partial class SectionItemViewModel : ObservableObject
    {
        private readonly ViewSection _view;
        private readonly bool _isPlaced;
        private readonly string _sheetNumber;
        private readonly string _placementScope;

        public ViewSection View => _view;
        public string ViewName => _view?.Name ?? "Unknown";
        public int Scale => _view?.Scale ?? 100;
        public string ViewId => _view?.Id?.ToString() ?? "-1";
        public ElementId ElementId => _view?.Id;
        public bool IsPlaced => _isPlaced;
        public string SheetNumber => _sheetNumber ?? string.Empty;
        public string PlacementScope => _placementScope ?? string.Empty;

        [ObservableProperty]
        private bool _isSelected = true;

        public SectionItemViewModel(
            ViewSection view,
            bool isPlaced,
            string sheetNumber,
            string placementScope)
        {
            _view = view ?? throw new ArgumentNullException(nameof(view));
            _isPlaced = isPlaced;
            _sheetNumber = sheetNumber ?? string.Empty;
            _placementScope = placementScope ?? string.Empty;
        }

        public string ScaleDisplay => $"1 : {Scale}";

        public string PlacementStatus
        {
            get
            {
                if (_isPlaced)
                {
                    return !string.IsNullOrEmpty(_sheetNumber)
                        ? $"Placed on Sheet {_sheetNumber}"
                        : "Placed (Unknown Sheet)";
                }
                return "Not Placed";
            }
        }

        public string SectionSummary => $"{ViewName} | Scale: 1:{Scale} | {PlacementStatus}";

        public void Refresh()
        {
            OnPropertyChanged(nameof(ViewName));
            OnPropertyChanged(nameof(Scale));
            OnPropertyChanged(nameof(ScaleDisplay));
            OnPropertyChanged(nameof(PlacementStatus));
            OnPropertyChanged(nameof(SectionSummary));
        }

        public override bool Equals(object obj)
        {
            return obj is SectionItemViewModel other &&
                   _view?.Id?.Value == other._view?.Id?.Value;
        }

        public override int GetHashCode() => _view?.Id?.Value.GetHashCode() ?? 0;
        public override string ToString() => $"{ViewName} (ID: {ViewId})";
    }
}