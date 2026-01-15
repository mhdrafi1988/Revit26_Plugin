using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Revit26_Plugin.Creaser_adv_V001.Models;
using Revit26_Plugin.Creaser_adv_V001.Services;

namespace Revit26_Plugin.Creaser_adv_V001.ViewModels
{
    public class CreaserAdvViewModel : ObservableObject
    {
        private readonly UIApplication _uiApp;
        private readonly UIDocument _uiDoc;
        private readonly Document _doc;
        private readonly ViewPlan _planView;
        private readonly FootPrintRoof _roof;

        public ObservableCollection<string> Log { get; } = new();

        public string RoofInfo =>
            $"Selected Roof: {_roof.Name} (Id {_roof.Id.Value})";

        public ObservableCollection<FamilySymbol> DetailItemSymbols { get; }

        private FamilySymbol _selectedDetailItem;
        public FamilySymbol SelectedDetailItem
        {
            get => _selectedDetailItem;
            set => SetProperty(ref _selectedDetailItem, value);
        }

        public IRelayCommand RunCommand { get; }

        public CreaserAdvViewModel(
            UIApplication uiApp,
            FootPrintRoof roof)
        {
            _uiApp = uiApp ?? throw new ArgumentNullException(nameof(uiApp));
            _uiDoc = _uiApp.ActiveUIDocument
                ?? throw new InvalidOperationException("No active document.");
            _doc = _uiDoc.Document;

            _planView = _doc.ActiveView as ViewPlan
                ?? throw new InvalidOperationException("Active view is not a plan view.");

            _roof = roof ?? throw new ArgumentNullException(nameof(roof));

            DetailItemSymbols =
                new ObservableCollection<FamilySymbol>(
                    new FilteredElementCollector(_doc)
                        .OfClass(typeof(FamilySymbol))
                        .OfCategory(BuiltInCategory.OST_DetailComponents)
                        .Cast<FamilySymbol>()
                        .OrderBy(s => s.FamilyName)
                        .ThenBy(s => s.Name));

            SelectedDetailItem = DetailItemSymbols.FirstOrDefault();

            RunCommand = new RelayCommand(Run, CanRun);

            LogMessage("Creaser Advanced initialized.");
            LogMessage(RoofInfo);
            LogMessage("Pathfinding: Dijkstra");
        }

        private bool CanRun()
        {
            return SelectedDetailItem != null;
        }

        private void Run()
        {
            try
            {
                Log.Clear();
                LogMessage("Running Creaser Advanced...");

                var graphBuilder = new RoofGraphBuilderService();
                RoofGraph graph = graphBuilder.Build(_roof);

                if (!graph.CornerNodes.Any() || !graph.DrainNodes.Any())
                {
                    LogMessage("No valid corner or drain nodes found.");
                    return;
                }

                LogMessage(
                    $"Graph built: {graph.Nodes.Count} nodes, " +
                    $"{graph.CornerNodes.Count} corners, " +
                    $"{graph.DrainNodes.Count} drains.");

                var strategy = new DijkstraPathFindingStrategy();
                var pathResults = new List<PathResult>();

                foreach (GraphNode corner in graph.CornerNodes)
                {
                    PathResult result = strategy.FindPath(graph, corner);

                    if (!result.PathFound)
                    {
                        LogMessage(
                            $"FAILED from Corner {corner.Id}: {result.FailureReason}");
                        continue;
                    }

                    pathResults.Add(result);

                    LogMessage(
                        $"SUCCESS: Corner {corner.Id} → Drain {result.EndNode.Id}");
                }

                if (!pathResults.Any())
                {
                    LogMessage("No valid drainage paths found.");
                    return;
                }

                IList<DrainPathSegment> segments;

                using (Transaction tx =
                    new Transaction(_doc, "Create Drainage Detail Lines"))
                {
                    tx.Start();

                    segments =
                        new DetailLineCreationService()
                            .CreateDetailLines(
                                _doc,
                                _planView,
                                pathResults);

                    tx.Commit();
                }

                LogMessage($"Detail lines created: {segments.Count}");

                if (segments.Count == 0)
                {
                    LogMessage("No detail lines created. Aborting.");
                    return;
                }

                using (Transaction tx =
                    new Transaction(_doc, "Place Drainage Detail Items"))
                {
                    tx.Start();

                    var placementService =
                        new DetailItemPlacementService(_doc, _planView);

                    var detailCurves =
                        segments
                            .Where(s => s.DetailCurve != null)
                            .Select(s => s.DetailCurve)
                            .ToList();

                    var placedIds =
                        placementService.Place(
                            detailCurves,
                            SelectedDetailItem);

                    tx.Commit();

                    LogMessage(
                        $"Detail items placed: {placedIds.Count}");
                }

                LogMessage("Creaser Advanced completed successfully.");
            }
            catch (Exception ex)
            {
                LogMessage($"ERROR: {ex.Message}");
            }
        }

        private void LogMessage(string message)
        {
            Log.Add($"[{DateTime.Now:HH:mm:ss}] {message}");
        }
    }
}