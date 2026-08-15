// File: SectionItemViewModel.cs
using System;
using Autodesk.Revit.DB;
using CommunityToolkit.Mvvm.ComponentModel;
using Revit26_Plugin.APUS.V322.Services;

namespace Revit26_Plugin.APUS.V322.ViewModels
{
    /// <summary>
    /// ViewModel for a single row in the Sections grid.
    /// V321 changes vs V320:
    ///  - PlacementScope removed (Whole Model is the only scope now).
    ///  - SizeDisplay added for the new Size (W x H) grid column.
    ///  - IsPlaced drives the grid's grey-out/lock behavior in XAML via a
    ///    converter bound directly to this property — placed rows are not
    ///    selectable for re-placement.
    /// </summary>
    public partial class SectionItemViewModel : ObservableObject
    {
        private readonly ViewSection _view;
        private readonly bool _isPlaced;
        private readonly string _sheetNumber;

        public ViewSection View => _view;

        public string ViewName => _view?.Name ?? "Unknown";

        public int Scale => _view?.Scale ?? 100;

        public string ViewId => _view?.Id?.ToString() ?? "-1";

        public ElementId ElementId => _view?.Id;

        /// <summary>Placed sections are greyed and locked against re-selection in the UI.</summary>
        public bool IsPlaced => _isPlaced;

        public string SheetNumber => _sheetNumber ?? string.Empty;

        public string DetailNumber
        {
            get
            {
                if (_view != null && _view.IsValidObject && _view.GetPrimaryViewId() != ElementId.InvalidElementId)
                {
                    var param = _view.get_Parameter(BuiltInParameter.VIEWPORT_DETAIL_NUMBER);
                    return param?.AsString() ?? string.Empty;
                }
                return string.Empty;
            }
        }

        /// <summary>Real rendered footprint (feet), computed lazily from crop box — full GetBoxOutline sizing happens at placement time via a temp viewport.</summary>
        private SectionFootprintDisplay? _footprintDisplay;
        private SectionFootprintDisplay FootprintDisplay
        {
            get
            {
                if (_footprintDisplay == null)
                {
                    _footprintDisplay = ComputeApproximateFootprint();
                }
                return _footprintDisplay.Value;
            }
        }

        /// <summary>"W x H" in meters, shown in the grid's Size column. This is an approximate (crop-box) size for browsing purposes only — the placement engine re-measures with GetBoxOutline() at run time, which is the authoritative size.</summary>
        public string SizeDisplay => FootprintDisplay.Display;

        public double SizeWidthM => FootprintDisplay.WidthM;
        public double SizeHeightM => FootprintDisplay.HeightM;

        [ObservableProperty]
        private bool _isSelected = true;

        public bool IsValid => _view != null && _view.IsValidObject;

        public SectionItemViewModel(ViewSection view, bool isPlaced, string sheetNumber)
        {
            _view = view ?? throw new ArgumentNullException(nameof(view));
            _isPlaced = isPlaced;
            _sheetNumber = sheetNumber ?? string.Empty;
        }

        public string ScaleDisplay => $"1 : {Scale}";

        public BoundingBoxXYZ GetSectionBounds() => _view?.CropBox;

        public XYZ ViewDirection => _view?.ViewDirection ?? XYZ.BasisY;
        public XYZ RightDirection => _view?.RightDirection ?? XYZ.BasisX;
        public XYZ UpDirection => _view?.UpDirection ?? XYZ.BasisZ;

        public string PlacementStatus => _isPlaced
            ? (!string.IsNullOrEmpty(_sheetNumber) ? $"Placed on Sheet {_sheetNumber}" : "Placed (Unknown Sheet)")
            : "Not Placed";

        public void Refresh()
        {
            _footprintDisplay = null;
            OnPropertyChanged(nameof(ViewName));
            OnPropertyChanged(nameof(Scale));
            OnPropertyChanged(nameof(ScaleDisplay));
            OnPropertyChanged(nameof(DetailNumber));
            OnPropertyChanged(nameof(PlacementStatus));
            OnPropertyChanged(nameof(SizeDisplay));
        }

        public override bool Equals(object obj)
        {
            if (obj is SectionItemViewModel other)
                return _view?.Id?.Value == other._view?.Id?.Value;
            return false;
        }

        public override int GetHashCode() => _view?.Id?.Value.GetHashCode() ?? 0;

        public override string ToString() => $"{ViewName} (ID: {ViewId})";

        private SectionFootprintDisplay ComputeApproximateFootprint()
        {
            var bb = _view?.CropBox;
            if (bb == null || _view == null)
                return new SectionFootprintDisplay(0, 0);

            double modelW = bb.Max.X - bb.Min.X;
            double modelH = bb.Max.Y - bb.Min.Y;
            int scale = _view.Scale > 0 ? _view.Scale : 1;

            double wFt = modelW / scale;
            double hFt = modelH / scale;

            double wM = Helpers.UnitConversionHelper.FeetToMm(wFt) / 1000.0;
            double hM = Helpers.UnitConversionHelper.FeetToMm(hFt) / 1000.0;

            return new SectionFootprintDisplay(wM, hM);
        }

        private readonly struct SectionFootprintDisplay
        {
            public double WidthM { get; }
            public double HeightM { get; }
            public string Display => $"{WidthM:F1}\u00d7{HeightM:F1}";

            public SectionFootprintDisplay(double widthM, double heightM)
            {
                WidthM = widthM;
                HeightM = heightM;
            }
        }
    }
}
