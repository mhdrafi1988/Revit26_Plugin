using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;

namespace Revit26_Plugin.Creaser_V03_03.ViewModels
{
    public partial class CreaserViewModel : ObservableObject
    {
        private readonly UIDocument _uiDoc;
        private readonly RoofBase _roof;

        public Helpers.UiLogService Logger { get; } = new();
        public ObservableCollection<string> LogMessages => Logger.Messages;
        public ObservableCollection<FamilySymbol> LineBasedFamilies { get; }

        [ObservableProperty]
        private FamilySymbol selectedFamily;

        public CreaserViewModel(UIDocument uiDoc, RoofBase roof)
        {
            _uiDoc = uiDoc;
            _roof = roof;

            Logger.Log("CreaserViewModel initialized");
            Logger.Log($"Roof Id: {_roof.Id}");

            LineBasedFamilies =
                Services.DetailItemPlacementService
                .GetLineBasedFamilies(uiDoc.Document, Logger);
        }

        [RelayCommand]
        private void Run()
        {
            Logger.Log("RUN started");

            Services.SlabShapeService.EnableEditing(
                _uiDoc.Document, _roof, Logger);

            var paths =
                Services.DrainPathService
                .ComputeDrainPaths(
                    _uiDoc.Document, _roof, Logger);

            Services.DetailItemPlacementService
                .PlaceDetailItems(
                    _uiDoc.Document,
                    _uiDoc.ActiveView,
                    SelectedFamily,
                    paths,
                    Logger);

            Logger.Log("RUN finished");
        }
    }
}
