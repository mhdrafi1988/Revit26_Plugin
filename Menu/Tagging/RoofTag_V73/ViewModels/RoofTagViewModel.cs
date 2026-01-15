using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Revit22_Plugin.RoofTagV3;
using Revit26_Plugin.RoofTag_V73.Models;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;

namespace Revit26_Plugin.RoofTag_V73.ViewModels
{
    public class RoofTagViewModel : INotifyPropertyChanged
    {
        private readonly Document _doc;

        public bool UseManualMode { get; set; }

        public bool IsAngle45 { get; set; } = true;
        public double SelectedAngle => IsAngle45 ? 45.0 : 30.0;

        public bool BendInward { get; set; } = true;

        public double BendOffset { get; set; } = 1000.0;
        public double EndOffset { get; set; } = 2000.0;

        public double BendOffsetFt =>
            UnitUtils.ConvertToInternalUnits(BendOffset, UnitTypeId.Millimeters);

        public double EndOffsetFt =>
            UnitUtils.ConvertToInternalUnits(EndOffset, UnitTypeId.Millimeters);

        public bool UseLeader { get; set; } = true;

        public ObservableCollection<SpotTagTypeWrapper> SpotTagTypes { get; }
        public SpotTagTypeWrapper SelectedSpotTagType { get; set; }

        public RoofTagViewModel(UIApplication uiApp)
        {
            _doc = uiApp.ActiveUIDocument.Document;

            SpotTagTypes = new ObservableCollection<SpotTagTypeWrapper>(
                new FilteredElementCollector(_doc)
                    .OfClass(typeof(SpotDimensionType))
                    .Cast<SpotDimensionType>()
                    .Select(t => new SpotTagTypeWrapper(t)));

            SelectedSpotTagType = SpotTagTypes.FirstOrDefault();
        }

        public event PropertyChangedEventHandler PropertyChanged;
        private void OnPropertyChanged([CallerMemberName] string name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
