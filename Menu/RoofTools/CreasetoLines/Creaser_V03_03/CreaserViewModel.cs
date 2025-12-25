using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
//using Revit26_Plugin.AutoLiner_V04.Services;
using Revit26_Plugin.Creaser_V03_03.Helpers;
using Revit26_Plugin.Creaser_V03_03.Models;
using Revit26_Plugin.Creaser_V03_03.Services;
using System.Collections.ObjectModel;
using Revit26_Plugin.Creaser_V03_03.Helpers;

namespace Revit26_Plugin.Creaser_V03_03.ViewModels
{
    public partial class CreaserViewModel : ObservableObject
    {
        private readonly UIDocument _uiDoc;
        private readonly RoofBase _roof;

        public UiLogService Logger { get; } = new();
        public ObservableCollection<string> LogMessages => Logger.Messages;

        public ObservableCollection<FamilySymbol> LineBasedFamilies { get; }

        [ObservableProperty]
        private FamilySymbol selectedFamily;

        public CreaserViewModel(UIDocument uiDoc, RoofBase roof)
        {
            _uiDoc = uiDoc;
            _roof = roof;

            Logger.Log("Creaser initialized");

            LineBasedFamilies =
                DetailItemPlacementService
                .GetLineBasedFamilies(uiDoc.Document, Logger);
        }

        [RelayCommand]
        private void Run()
        {
            Logger.Log("Run started");

            SlabShapeService.EnableEditing(
                _uiDoc.Document,
                _roof,
                Logger);

            var paths =
                DrainPathService.ComputeDrainPaths(
                    _uiDoc.Document,
                    _roof,
                    Logger);

            DetailItemPlacementService.PlaceDetailItems(
                _uiDoc.Document,
                _uiDoc.ActiveView,
                SelectedFamily,
                paths,
                Logger);

            Logger.Log("Run finished");
        }
    }
}
