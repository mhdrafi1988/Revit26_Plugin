// CreaserAdvViewModel.cs
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Revit26_Plugin.CreaserAdv_V00.Services.DetailItems;
using Revit26_Plugin.CreaserAdv_V00.Services.Geometry;
using Revit26_Plugin.CreaserAdv_V00.Services.Logging;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace Revit26_Plugin.CreaserAdv_V00.ViewModels
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
            var symbolCollector = new DetailItemCollectorService(_doc);

            DetailSymbols.Clear();
            foreach (FamilySymbol s in symbolCollector.Collect())
                DetailSymbols.Add(s);

            if (DetailSymbols.Count > 0)
                SelectedDetailSymbol = DetailSymbols[0];
        }

        // =========================
        // MAIN COMMAND PIPELINE
        // =========================
        [RelayCommand]
        private void Run()
        {
            if (SelectedDetailSymbol == null)
                return;

            if (_doc.ActiveView is not ViewPlan planView)
                return;

            using var tx = new Transaction(_doc, "Creaser Advanced");
            tx.Start();

            // ============================
            // 1?? Extract LINEAR creases
            // ============================
            var creaseExtractor =
                new RoofSharedTopFaceCreaseService();

            IList<Line> linearCreases =
                creaseExtractor.ExtractNormalizedCreaseLines(_roof);

            // ============================
            // 2?? Extract CURVED creases
            // ============================
            IList<Line> curvedCreases =
                CurvedCreaseExtractionHelper
                    .ExtractCurvedCreaseSegments(_roof);

            // ============================
            // 3?? Merge ALL creases (3D)
            // ============================
            var allCreases3d = new List<Line>();
            allCreases3d.AddRange(linearCreases);
            allCreases3d.AddRange(curvedCreases);

            // ============================
            // 4?? Project to plan (order preserved)
            // ============================
            var planLines = new List<Line>();
            foreach (Line l in allCreases3d)
            {
                planLines.Add(
                    CreasePlanProjectionHelper.ProjectToPlan(l, planView));
            }

            // ============================
            // 5?? Optional filters (corner/profile)
            // ============================
            planLines =
                CornerDrainPathFilterService
                    .KeepShortestPerCorner(planLines)
                    .ToList();

            // ============================
            // 6?? Place detail items
            // ============================
            var placer =
                new DetailItemPlacementService(_doc, planView);

            placer.PlaceAlongLines(
                planLines,
                SelectedDetailSymbol,
                _log);

            tx.Commit();
        }


        [RelayCommand]
        private void ClearLog()
        {
            _log.Clear();
        }
    }
}
