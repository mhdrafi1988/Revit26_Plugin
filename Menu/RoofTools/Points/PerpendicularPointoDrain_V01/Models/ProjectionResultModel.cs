using Autodesk.Revit.DB;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace Revit26_Plugin.PerpendicularPointoDrain.V01.Models
{
    /// <summary>
    /// Represents one candidate (drain group × compass direction × loop) and, after Apply,
    /// the outcome of trying to create a shape point there. Status is notifying because
    /// Analyze creates rows as "Pending" and Apply updates them in place.
    /// </summary>
    public class ProjectionResultModel : INotifyPropertyChanged
    {
        public string GroupLabel { get; set; }
        public string Direction  { get; set; }    // N / NE / E / SE / S / SW / W / NW / "-"
        public string LoopLabel  { get; set; }     // "Outer" / "Inner #1" / ...
        public double DistanceMm { get; set; }
        public XYZ    Point      { get; set; }     // null for warning rows with no valid candidate
        public bool   IsFallback { get; set; }      // true if no exact-sector hit, nearest edge used instead

        private string _status = "Pending";
        public string Status
        {
            get => _status;
            set { if (_status != value) { _status = value; OnPropertyChanged(); } }
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
