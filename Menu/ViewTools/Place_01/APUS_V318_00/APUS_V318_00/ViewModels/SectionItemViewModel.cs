using System; // Add this at the top of the file

// File: SectionItemViewModel.cs
using Autodesk.Revit.DB;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Revit26_Plugin.APUS_V318.ViewModels
{
    /// <summary>
    /// ViewModel for representing a section view in the UI
    /// </summary>
    public partial class SectionItemViewModel : ObservableObject
    {
        private readonly ViewSection _view;
        private readonly bool _isPlaced;
        private readonly string _sheetNumber;
        private readonly string _placementScope;

        /// <summary>
        /// Gets the underlying Revit ViewSection
        /// </summary>
        public ViewSection View => _view;

        /// <summary>
        /// Gets the name of the section view
        /// </summary>
        public string ViewName => _view?.Name ?? "Unknown";

        /// <summary>
        /// Gets the scale of the section view
        /// </summary>
        public int Scale => _view?.Scale ?? 100;

        /// <summary>
        /// Gets the unique ID of the section view
        /// </summary>
        public string ViewId => _view?.Id?.ToString() ?? "-1";

        /// <summary>
        /// Gets the ElementId of the section view
        /// </summary>
        public ElementId ElementId => _view?.Id;

        /// <summary>
        /// Gets a value indicating whether the section is placed on a sheet
        /// </summary>
        public bool IsPlaced => _isPlaced;

        /// <summary>
        /// Gets the sheet number where the section is placed (if any)
        /// </summary>
        public string SheetNumber => _sheetNumber ?? string.Empty;

        /// <summary>
        /// Gets the placement scope information
        /// </summary>
        public string PlacementScope => _placementScope ?? string.Empty;

        /// <summary>
        /// Gets the detail number of the section (if placed on sheet)
        /// </summary>
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

        /// <summary>
        /// Gets the section head location in project coordinates
        /// </summary>
        public XYZ SectionHeadLocation
        {
            get
            {
                if (_view != null && _view.IsValidObject)
                {
                    // Get the section head location from the crop box
                    var bbox = _view.CropBox;
                    if (bbox != null)
                    {
                        return bbox.Min;
                    }
                }
                return XYZ.Zero;
            }
        }

        /// <summary>
        /// Gets or sets whether this section is selected in the UI
        /// </summary>
        [ObservableProperty]
        private bool _isSelected = true;

        /// <summary>
        /// Gets a value indicating whether the view is valid and can be used
        /// </summary>
        public bool IsValid => _view != null && _view.IsValidObject;

        /// <summary>
        /// Gets the view's discipline
        /// </summary>
        public string Discipline
        {
            get
            {
                if (_view != null && _view.IsValidObject)
                {
                    var param = _view.get_Parameter(BuiltInParameter.VIEW_DISCIPLINE);
                    if (param != null && param.HasValue)
                    {
                        return param.AsValueString() ?? param.AsInteger().ToString();
                    }
                }
                return "Unknown";
            }
        }

        /// <summary>
        /// Initializes a new instance of the SectionItemViewModel class
        /// </summary>
        /// <param name="view">The Revit ViewSection</param>
        /// <param name="isPlaced">Whether the section is placed on a sheet</param>
        /// <param name="sheetNumber">The sheet number where placed</param>
        /// <param name="placementScope">Additional placement information</param>
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

        /// <summary>
        /// Gets display information about the section's scale
        /// </summary>
        public string ScaleDisplay => $"1 : {Scale}";

        /// <summary>
        /// Gets the section's bounding box in project coordinates
        /// </summary>
        public BoundingBoxXYZ GetSectionBounds()
        {
            return _view?.CropBox;
        }

        /// <summary>
        /// Gets the section's view direction
        /// </summary>
        public XYZ ViewDirection => _view?.ViewDirection ?? XYZ.BasisY;

        /// <summary>
        /// Gets the section's right direction
        /// </summary>
        public XYZ RightDirection => _view?.RightDirection ?? XYZ.BasisX;

        /// <summary>
        /// Gets the section's up direction
        /// </summary>
        public XYZ UpDirection => _view?.UpDirection ?? XYZ.BasisZ;

        /// <summary>
        /// Gets information about whether the section can be placed
        /// </summary>
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

        /// <summary>
        /// Gets a summary of section properties
        /// </summary>
        public string SectionSummary
        {
            get
            {
                return $"{ViewName} | Scale: 1:{Scale} | {PlacementStatus}";
            }
        }

        /// <summary>
        /// Refreshes the view data (call when underlying Revit view may have changed)
        /// </summary>
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

        /// <summary>
        /// Compares two SectionItemViewModel instances for equality
        /// </summary>
        public override bool Equals(object obj)
        {
            if (obj is SectionItemViewModel other)
            {
                return _view?.Id?.Value == other._view?.Id?.Value;
            }
            return false;
        }

        /// <summary>
        /// Gets the hash code for this instance
        /// </summary>
        public override int GetHashCode()
        {
            return _view?.Id?.Value.GetHashCode() ?? 0;
        }

        /// <summary>
        /// Returns a string representation of the section
        /// </summary>
        public override string ToString()
        {
            return $"{ViewName} (ID: {ViewId})";
        }
        // Add to SectionItemViewModel.cs - for tracking sort positions in logs

        private double _sortX;
        /// <summary>
        /// X coordinate for sorting (set during reading order calculation)
        /// </summary>
        public double SortX
        {
            get => _sortX;
            set => SetProperty(ref _sortX, value);
        }

        private double _sortY;
        /// <summary>
        /// Y coordinate for sorting (set during reading order calculation)
        /// </summary>
        public double SortY
        {
            get => _sortY;
            set => SetProperty(ref _sortY, value);
        }

        private int _sortIndex;
        /// <summary>
        /// Position in sorted order (1-based)
        /// </summary>
        public int SortIndex
        {
            get => _sortIndex;
            set => SetProperty(ref _sortIndex, value);
        }
    }

}