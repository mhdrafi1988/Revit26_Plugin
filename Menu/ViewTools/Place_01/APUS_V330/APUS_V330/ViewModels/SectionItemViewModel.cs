// File: ViewModels/SectionItemViewModel.cs
using System;
using Autodesk.Revit.DB;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Revit26_Plugin.APUS_V330.ViewModels
{
    public partial class SectionItemViewModel : ObservableObject
    {
        private readonly ViewSection _view;
        private readonly bool        _isPlaced;
        private readonly string      _sheetNumber;
        private readonly string      _placementScope;

        public ViewSection View       => _view;
        public string      ViewName   => _view?.Name ?? "Unknown";
        public int         Scale      => _view?.Scale ?? 100;
        public string      ViewId     => _view?.Id?.ToString() ?? "-1";
        public ElementId   ElementId  => _view?.Id;
        public bool        IsPlaced   => _isPlaced;
        public string      SheetNumber    => _sheetNumber    ?? string.Empty;
        public string      PlacementScope => _placementScope ?? string.Empty;

        public string DetailNumber
        {
            get
            {
                if (_view != null && _view.IsValidObject &&
                    _view.GetPrimaryViewId() != ElementId.InvalidElementId)
                {
                    var param = _view.get_Parameter(BuiltInParameter.VIEWPORT_DETAIL_NUMBER);
                    return param?.AsString() ?? string.Empty;
                }
                return string.Empty;
            }
        }

        public XYZ SectionHeadLocation
        {
            get
            {
                if (_view != null && _view.IsValidObject)
                {
                    var bbox = _view.CropBox;
                    if (bbox != null) return bbox.Min;
                }
                return XYZ.Zero;
            }
        }

        /// <summary>
        /// Drives the checkbox in the DataGrid. Toggling this does NOT affect
        /// the WPF row-selection highlight — the two are intentionally decoupled.
        /// </summary>
        [ObservableProperty]
        private bool _isSelected = true;

        public bool   IsValid        => _view != null && _view.IsValidObject;
        public string ScaleDisplay   => $"1 : {Scale}";
        public string PlacementStatus =>
            _isPlaced
                ? (!string.IsNullOrEmpty(_sheetNumber) ? $"Placed on Sheet {_sheetNumber}" : "Placed (Unknown Sheet)")
                : "Not Placed";
        public string SectionSummary => $"{ViewName} | Scale: 1:{Scale} | {PlacementStatus}";

        public string Discipline
        {
            get
            {
                if (_view != null && _view.IsValidObject)
                {
                    var param = _view.get_Parameter(BuiltInParameter.VIEW_DISCIPLINE);
                    if (param != null && param.HasValue)
                        return param.AsValueString() ?? param.AsInteger().ToString();
                }
                return "Unknown";
            }
        }

        // Sort coordinates set by the reading-order stage
        private double _sortX;
        public double SortX { get => _sortX; set => SetProperty(ref _sortX, value); }

        private double _sortY;
        public double SortY { get => _sortY; set => SetProperty(ref _sortY, value); }

        private int _sortIndex;
        public int SortIndex { get => _sortIndex; set => SetProperty(ref _sortIndex, value); }

        public SectionItemViewModel(
            ViewSection view,
            bool        isPlaced,
            string      sheetNumber,
            string      placementScope)
        {
            _view           = view ?? throw new ArgumentNullException(nameof(view));
            _isPlaced       = isPlaced;
            _sheetNumber    = sheetNumber    ?? string.Empty;
            _placementScope = placementScope ?? string.Empty;
        }

        public BoundingBoxXYZ GetSectionBounds() => _view?.CropBox;

        public XYZ ViewDirection  => _view?.ViewDirection  ?? XYZ.BasisY;
        public XYZ RightDirection => _view?.RightDirection ?? XYZ.BasisX;
        public XYZ UpDirection    => _view?.UpDirection    ?? XYZ.BasisZ;

        public void Refresh()
        {
            OnPropertyChanged(nameof(ViewName));
            OnPropertyChanged(nameof(Scale));
            OnPropertyChanged(nameof(ScaleDisplay));
            OnPropertyChanged(nameof(Discipline));
            OnPropertyChanged(nameof(DetailNumber));
            OnPropertyChanged(nameof(PlacementStatus));
            OnPropertyChanged(nameof(SectionSummary));
        }

        public override bool Equals(object obj)
        {
            if (obj is SectionItemViewModel other)
                return _view?.Id?.Value == other._view?.Id?.Value;
            return false;
        }

        public override int GetHashCode() => _view?.Id?.Value.GetHashCode() ?? 0;
        public override string ToString()  => $"{ViewName} (ID: {ViewId})";
    }
}
