using Autodesk.Revit.DB;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace Revit26_Plugin.PonitOnCurvesInnerandOuter.V01.Models
{
    public enum DivisionMode
    {
        ByCount,
        ByDistance
    }

    public class RoofLoopModel : INotifyPropertyChanged
    {
        // ── Identity ──────────────────────────────────────────────────────────────
        public int Index { get; set; }
        public string LoopType { get; set; }   // "Outer" / "Inner"
        public string LoopShapeType { get; set; }   // "Circular" / "Oval" / "Arc" / "Rectangle" / "Other"
        public bool IsCircular { get; set; }
        public bool HasCurvedSegments { get; set; }

        // ── Geometry (metres, for display) ───────────────────────────────────────
        public double PerimeterM { get; set; }
        public double CurvedLengthM { get; set; }
        public double RadiusM { get; set; }

        // ── Revit internal geometry ───────────────────────────────────────────────
        public XYZ Center { get; set; }
        public double Radius { get; set; }
        public CurveLoop Geometry { get; set; }

        // ── Grouping helper ───────────────────────────────────────────────────────
        public string GroupKey => $"{LoopType}  —  {LoopShapeType}";

        // ── Selection ─────────────────────────────────────────────────────────────
        private bool _isSelected = true;
        public bool IsSelected
        {
            get => _isSelected;
            set { if (_isSelected != value) { _isSelected = value; OnPropertyChanged(); OnPropertyChanged(nameof(RowBrush)); } }
        }

        // ── Division mode ─────────────────────────────────────────────────────────
        private DivisionMode _mode = DivisionMode.ByCount;
        public DivisionMode Mode
        {
            get => _mode;
            set
            {
                if (_mode != value)
                {
                    _mode = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(IsCountMode));
                    OnPropertyChanged(nameof(IsDistanceMode));
                    RecalcPoints();
                }
            }
        }

        // Writable bool wrappers so TwoWay binding on ToggleButton works
        public bool IsCountMode
        {
            get => _mode == DivisionMode.ByCount;
            set { if (value && _mode != DivisionMode.ByCount) Mode = DivisionMode.ByCount; }
        }

        public bool IsDistanceMode
        {
            get => _mode == DivisionMode.ByDistance;
            set { if (value && _mode != DivisionMode.ByDistance) Mode = DivisionMode.ByDistance; }
        }

        // ── By-Count input ────────────────────────────────────────────────────────
        private int _manualCount = 8;
        public int ManualCount
        {
            get => _manualCount;
            set { if (_manualCount != value) { _manualCount = value < 1 ? 1 : value; OnPropertyChanged(); RecalcPoints(); } }
        }

        // ── By-Distance inputs ────────────────────────────────────────────────────
        private double _maxSpacingM = 0.5;
        public double MaxSpacingM
        {
            get => _maxSpacingM;
            set { if (_maxSpacingM != value) { _maxSpacingM = value <= 0 ? 0.1 : value; OnPropertyChanged(); RecalcPoints(); } }
        }

        private double _fixedSpacingM = 0.5;
        public double FixedSpacingM
        {
            get => _fixedSpacingM;
            set { if (_fixedSpacingM != value) { _fixedSpacingM = value <= 0 ? 0.1 : value; OnPropertyChanged(); RecalcPoints(); } }
        }

        // ── Computed point count ───────────────────────────────────────────────────
        private int _recommendedPoints = 8;
        public int RecommendedPoints
        {
            get => _recommendedPoints;
            set { if (_recommendedPoints != value) { _recommendedPoints = value; OnPropertyChanged(); } }
        }

        // ── Row highlight ──────────────────────────────────────────────────────────
        public string RowBrush => _isSelected ? "#C8E6C9" : "Transparent";

        // ── RecalcPoints ───────────────────────────────────────────────────────────
        public void RecalcPoints()
        {
            if (_mode == DivisionMode.ByCount)
            {
                RecommendedPoints = System.Math.Max(1, _manualCount);
            }
            else
            {
                // MaxSpacingM  → ceiling division (at least one point every N metres)
                int fromMax   = PerimeterM > 0 && _maxSpacingM > 0
                    ? System.Math.Max(2, (int)System.Math.Ceiling(PerimeterM / _maxSpacingM))
                    : 2;

                // FixedSpacingM → round to nearest whole number of equally-spaced points
                int fromFixed = PerimeterM > 0 && _fixedSpacingM > 0
                    ? System.Math.Max(2, (int)System.Math.Round(PerimeterM / _fixedSpacingM))
                    : 2;

                // Use the larger value so both constraints are satisfied
                RecommendedPoints = System.Math.Max(fromMax, fromFixed);
            }
        }

        // ── INotifyPropertyChanged ────────────────────────────────────────────────
        public event PropertyChangedEventHandler PropertyChanged;
        protected virtual void OnPropertyChanged([CallerMemberName] string prop = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(prop));
    }
}