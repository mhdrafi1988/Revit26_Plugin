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
    /// <summary>
    /// ViewModel for Creaser Advanced.
    /// Roof is selected BEFORE UI launch (Revit-safe pattern).
    /// </summary>
    public class CreaserAdvViewModel : ObservableObject
    {
        private readonly UIApplication _uiApp;
        private readonly UIDocument _uiDoc;
        private readonly Document _doc;
        private readonly ViewPlan _planView;
        private readonly FootPrintRoof _roof;

        // =========================
        // UI STATE
        // =========================

        public ObservableCollection<string> Log { get; } = new();

        public string RoofInfo =>
            $"Selected Roof: {_roof.Name} (Id {_roof.Id.Value})";

        // =========================
        // PATHFINDING
        // =========================

        public Array PathFindingMethods =>
            Enum.GetValues(typeof(PathFindingMethod));

        private PathFindingMethod _selectedMethod = PathFindingMethod.FastGreedy;
        public PathFindingMethod SelectedMethod
        {
            get => _selectedMethod;
            set => SetProperty(ref _selectedMethod, value);
        }

        // =========================
        // DETAIL ITEM
        // =========================

        public ObservableCollection<FamilySymbol> DetailItemSymbols { get; }

        private FamilySymbol _selectedDetailItem;
        public FamilySymbol SelectedDetailItem
        {
            get => _selectedDetailItem;
            set => SetProperty(ref _selectedDetailItem, value);
        }

        // =========================
        // COMMANDS
        // =========================

        public IRelayCommand RunCommand { get; }

        // =========================
        // CONSTRUCTOR
        // =========================

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

            // Collect detail component symbols
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
        }

        private bool CanRun()
        {
            return SelectedDetailItem != null;
        }

        // =========================
        // MAIN EXECUTION
        // =========================

        private void Run()
        {
            try
            {
                Log.Clear();
                LogMessage("Running Creaser Advanced...");

                // -------------------------------------------------
                // 1. Build roof graph
                // -------------------------------------------------
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

                // -------------------------------------------------
                // 2. Resolve pathfinding strategy
                // -------------------------------------------------
                IPathFindingStrategy strategy = ResolveStrategy();
                LogMessage($"Pathfinding: {strategy.GetType().Name}");

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

                // -------------------------------------------------
                // 3. Create detail lines
                // -------------------------------------------------
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

                // -------------------------------------------------
                // 4. Place detail items (FIXED)
                // -------------------------------------------------
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

        // =========================
        // STRATEGY RESOLUTION
        // =========================

        private IPathFindingStrategy ResolveStrategy()
        {
            return SelectedMethod switch
            {
                PathFindingMethod.FastGreedy =>
                    new GreedyDownhillStrategy(),

                PathFindingMethod.AStar =>
                    new AStarPathFindingStrategy(),

                PathFindingMethod.Dijkstra =>
                    new DijkstraPathFindingStrategy(),

                _ => throw new InvalidOperationException(
                    "Invalid pathfinding method.")
            };
        }

        // =========================
        // LOGGING
        // =========================

        private void LogMessage(string message)
        {
            Log.Add($"[{DateTime.Now:HH:mm:ss}] {message}");
        }
    }
}
