using Autodesk.Revit.DB;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
//using Revit26_Plugin.AutoLiner_V04.Services;
using Revit26_Plugin.Creaser_V32.Helpers;
using Revit26_Plugin.Creaser_V32.Services;
using System.Collections.ObjectModel;
using System.Linq;

namespace Revit26_Plugin.Creaser_V32.ViewModels
{
    public class CreaserViewModel : ObservableObject
    {
        private readonly Document _doc;
        private readonly RoofBase _roof;
        private readonly UiLogService _log;

        public ObservableCollection<ElementType> LineBasedDetailTypes { get; }
        public ElementType SelectedDetailType { get; set; }

        public string LogText => _log.FullText;

        public IRelayCommand RunCommand { get; }

        public CreaserViewModel(Document doc, RoofBase roof, UiLogService log)
        {
            _doc = doc;
            _roof = roof;
            _log = log;

            LineBasedDetailTypes = new ObservableCollection<ElementType>(
                new FilteredElementCollector(doc)
                    .OfClass(typeof(FamilySymbol))
                    .Cast<FamilySymbol>()
                    .Where(x => x.Category != null &&
                                x.Category.Id != null &&
                                x.Category.Id.Equals(new ElementId((int)BuiltInCategory.OST_DetailComponents)) &&
                                x.Family.FamilyPlacementType == FamilyPlacementType.CurveBasedDetail));

            RunCommand = new RelayCommand(Execute);
        }

        private void Execute()
        {
            RoofGeometryService geoService = new RoofGeometryService(_log);
            CreasePathService pathService = new CreasePathService(_log);
            DetailItemPlacementService placeService = new DetailItemPlacementService(_log);

            var data = geoService.Extract(_doc, _roof);
            var paths = pathService.BuildPaths(data);
            var lines = pathService.ConvertToDirectedLines(paths);

            placeService.Place(_doc, lines, SelectedDetailType);
        }
    }
}
