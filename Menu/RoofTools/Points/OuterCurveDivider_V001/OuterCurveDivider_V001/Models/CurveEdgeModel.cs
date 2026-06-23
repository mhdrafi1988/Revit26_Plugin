using Autodesk.Revit.DB;
using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace Revit26_Plugin.OuterCurveDivider.V001.Models
{
    /// <summary>
    /// One non-linear edge of the picked roof (one grid row). Every edge resolves to a single
    /// <see cref="FinalPointCount"/> — the exact number of points Apply places. Count-driven
    /// edges use the editable, length-seeded <see cref="PointCount"/>; distance-driven edges
    /// compute it from spacing.
    /// </summary>
    public class CurveEdgeModel : INotifyPropertyChanged
    {
        public int    Index         { get; set; }
        public string CurveTypeName { get; set; }
        public double LengthM       { get; set; }
        public Curve  Geometry      { get; set; }
        public EdgeTypeSetting TypeSetting { get; set; }

        /// <summary>Length-bucket default number of POINTS, assigned at extraction.</summary>
        public int LengthDefaultPointCount { get; set; }

        private bool _isSelected = true;
        public bool IsSelected
        {
            get => _isSelected;
            set { if (_isSelected != value) { _isSelected = value; OnPropertyChanged(); } }
        }

        private int _pointCount;
        public int PointCount
        {
            get => _pointCount;
            set
            {
                int v = Math.Max(0, value);
                if (_pointCount != v) { _pointCount = v; OnPropertyChanged(); OnPropertyChanged(nameof(FinalPointCount)); }
            }
        }

        private bool _isManual;
        public bool IsManual
        {
            get => _isManual;
            set { if (_isManual != value) { _isManual = value; OnPropertyChanged(); } }
        }

        private bool _hasOverride;
        public bool HasOverride
        {
            get => _hasOverride;
            set
            {
                if (_hasOverride != value)
                {
                    _hasOverride = value;
                    if (value)
                    {
                        _overrideMode         = TypeSetting?.Mode         ?? DivisionMode.ByCount;
                        _overrideTargetMeters = TypeSetting?.TargetMeters ?? 0.50;
                        OnPropertyChanged(nameof(OverrideMode));
                        OnPropertyChanged(nameof(OverrideModeText));
                        OnPropertyChanged(nameof(OverrideTargetMeters));
                    }
                    OnPropertyChanged();
                    RaiseRuleChanged();
                }
            }
        }

        private DivisionMode _overrideMode = DivisionMode.ByCount;
        public DivisionMode OverrideMode
        {
            get => _overrideMode;
            set
            {
                if (_overrideMode != value)
                {
                    _overrideMode = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(OverrideModeText));
                    RaiseRuleChanged();
                }
            }
        }

        public string OverrideModeText
        {
            get => OverrideMode == DivisionMode.ByCount ? "Count" : "Distance";
            set => OverrideMode = string.Equals(value, "Count", StringComparison.OrdinalIgnoreCase)
                                    ? DivisionMode.ByCount
                                    : DivisionMode.ByDistance;
        }

        private double _overrideTargetMeters = 0.50;
        public double OverrideTargetMeters
        {
            get => _overrideTargetMeters;
            set { if (_overrideTargetMeters != value) { _overrideTargetMeters = value; OnPropertyChanged(); RaiseRuleChanged(); } }
        }

        public bool OverrideDistanceEnabled => HasOverride && OverrideMode == DivisionMode.ByDistance;

        public DivisionMode EffectiveMode         => HasOverride ? OverrideMode         : (TypeSetting?.Mode         ?? DivisionMode.ByCount);
        public double       EffectiveTargetMeters => HasOverride ? OverrideTargetMeters : (TypeSetting?.TargetMeters ?? 0.50);
        public bool         IsCountDriven         => EffectiveMode == DivisionMode.ByCount;

        private int DistancePoints
        {
            get
            {
                double t = EffectiveTargetMeters;
                if (t <= 1e-9) return 0;
                int segs = (int)Math.Round(LengthM / t, MidpointRounding.AwayFromZero);
                return Math.Max(0, segs - 1);
            }
        }

        /// <summary>Exact points placed on Apply (and shown/edited in the grid).</summary>
        public int FinalPointCount
        {
            get => IsCountDriven ? Math.Max(0, PointCount) : DistancePoints;
            set
            {
                if (IsCountDriven) { PointCount = value; IsManual = true; }
                OnPropertyChanged();
            }
        }

        public void NotifyInheritedRuleChanged() => RaiseRuleChanged();

        private void RaiseRuleChanged()
        {
            OnPropertyChanged(nameof(EffectiveMode));
            OnPropertyChanged(nameof(EffectiveTargetMeters));
            OnPropertyChanged(nameof(IsCountDriven));
            OnPropertyChanged(nameof(OverrideDistanceEnabled));
            OnPropertyChanged(nameof(FinalPointCount));
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string propertyName = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
