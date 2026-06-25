using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace Revit26_Plugin.OuterCurveDivider.V001.Models
{
    /// <summary>
    /// Default division rule for one curve type (e.g. all Arcs). Edges of that type follow
    /// this unless overridden or manually edited. Default mode is Count so each edge shows
    /// its length-seeded point count on open; the type's Count field bulk-fills that type's
    /// (non-manual) edges.
    /// </summary>
    public class EdgeTypeSetting : INotifyPropertyChanged
    {
        public string TypeName { get; set; }

        private bool _modeByDistance;
        public bool ModeByDistance
        {
            get => _modeByDistance;
            set
            {
                if (_modeByDistance != value)
                {
                    _modeByDistance = value;
                    OnPropertyChanged();
                    if (value && _modeByCount) { _modeByCount = false; OnPropertyChanged(nameof(ModeByCount)); }
                    OnPropertyChanged(nameof(Mode));
                }
            }
        }

        private bool _modeByCount = true;
        public bool ModeByCount
        {
            get => _modeByCount;
            set
            {
                if (_modeByCount != value)
                {
                    _modeByCount = value;
                    OnPropertyChanged();
                    if (value && _modeByDistance) { _modeByDistance = false; OnPropertyChanged(nameof(ModeByDistance)); }
                    OnPropertyChanged(nameof(Mode));
                }
            }
        }

        private double _targetMeters = 0.50;
        public double TargetMeters
        {
            get => _targetMeters;
            set { if (_targetMeters != value) { _targetMeters = value; OnPropertyChanged(); } }
        }

        private int _fixedCount = 8;
        public int FixedCount
        {
            get => _fixedCount;
            set { if (_fixedCount != value) { _fixedCount = value; OnPropertyChanged(); } }
        }

        public DivisionMode Mode => ModeByCount ? DivisionMode.ByCount : DivisionMode.ByDistance;

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string propertyName = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
