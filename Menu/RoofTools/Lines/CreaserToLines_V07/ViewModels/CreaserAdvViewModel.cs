// CreaserAdvViewModel.cs
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Revit26_Plugin.CreaserAdv_V00_701.Services.DetailItems;
using Revit26_Plugin.CreaserAdv_V00_701.Services.Geometry;
using Revit26_Plugin.CreaserAdv_V00_701.Services.Logging;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace Revit26_Plugin.CreaserAdv_V00_701.ViewModels
{
    public partial class CreaserAdvViewModel : ObservableObject
    {
        private readonly UIDocument _uiDoc;
        private readonly Document _doc;
        private readonly Element _roof;
        private readonly LoggingService _log;

        public ObservableCollection<FamilySymbol> DetailSymbols { get; } = new();
        public ObservableCollection<LogEntry> LogEntries => _log.Entries;

        private FamilySymbol _selectedDetailSymbol;
        public FamilySymbol SelectedDetailSymbol
        {
            get => _selectedDetailSymbol;
            set => SetProperty(ref _selectedDetailSymbol, value);
        }

        public CreaserAdvViewModel(
            UIApplication uiApp,
            Element roof,
            LoggingService log)
        {
            _uiDoc = uiApp.ActiveUIDocument;
            _doc = _uiDoc.Document;
            _roof = roof;
            _log = log;

            LoadDetailSymbols();
        }

        private void LoadDetailSymbols()
        {
            // Collect symbols (not lines). This is what your UI list is showing.
            var symbolCollector = new DetailItemSymbolCollectorService(_doc);

            DetailSymbols.Clear();
            foreach (FamilySymbol s in symbolCollector.CollectDetailSymbols(_log))
                DetailSymbols.Add(s);

            if (DetailSymbols.Count > 0)
                SelectedDetailSymbol = DetailSymbols[0];
        }

        [RelayCommand]
        private void Run()
        {
            if (SelectedDetailSymbol == null)
                return;

            if (_doc.ActiveView is not ViewPlan planView)
                return;

            using var tx = new Transaction(_doc, "Creaser Advanced");
            tx.Start();

            var creaseExtractor = new RoofSharedTopFaceCreaseService(_log);
            IList<Line> creases3d = creaseExtractor.ExtractSharedTopFaceCreases(_roof);

            var projector = new CreaseLineProjectionService(_log);
            IList<Line> creases2d = projector.ProjectToPlan(creases3d);

            var placer = new DetailItemPlacementService(_doc, planView);
            placer.PlaceAlongLines(creases2d, SelectedDetailSymbol, _log);

            tx.Commit();
        }

        [RelayCommand]
        private void ClearLog()
        {
            _log.Clear();
        }
    }
}
