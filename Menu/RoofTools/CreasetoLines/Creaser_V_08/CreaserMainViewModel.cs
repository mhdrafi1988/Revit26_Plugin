using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Text;
using System.Windows.Input;
using Autodesk.Revit.DB;
using Revit26_Plugin.Creaser_V08.Commands.GeometryHelpers;
using Revit26_Plugin.Creaser_V08.Commands.Models;
using Revit26_Plugin.Creaser_V08.Commands.Services;

namespace Revit26_Plugin.Creaser_V08.Commands.ViewModels
{
    public class CreaserMainViewModel : INotifyPropertyChanged
    {
        // ------------------------------------------------------------
        // Revit context
        // ------------------------------------------------------------

        private readonly Document _doc;
        private readonly Element _roof;
        private readonly View _view;
        private readonly Action _closeAction;

        // ------------------------------------------------------------
        // Logging
        // ------------------------------------------------------------

        private readonly StringBuilder _log = new();
        public string LogText => _log.ToString();

        // ------------------------------------------------------------
        // User inputs
        // ------------------------------------------------------------

        public string DrainRadiusMm { get; set; } = "1000";
        public string ToleranceMm { get; set; } = "5";

        /// <summary>
        /// true  = merge collinear creases
        /// false = force one crease per corner
        /// </summary>
        public bool MergeCollinearCreases { get; set; } = true;

        // ------------------------------------------------------------
        // Detail Item families
        // ------------------------------------------------------------

        public ObservableCollection<DetailItemSymbolInfo> DetailItemSymbols { get; }
            = new();

        public DetailItemSymbolInfo SelectedDetailItemSymbol { get; set; }

        // ------------------------------------------------------------
        // Commands
        // ------------------------------------------------------------

        public ICommand RunCommand { get; }
        public ICommand CloseCommand { get; }

        // ------------------------------------------------------------
        // INotifyPropertyChanged
        // ------------------------------------------------------------

        public event PropertyChangedEventHandler PropertyChanged;

        private void RaisePropertyChanged(string name)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }

        // ------------------------------------------------------------
        // Constructor
        // ------------------------------------------------------------

        public CreaserMainViewModel(
            Document document,
            Element roof,
            View view,
            Action closeAction)
        {
            _doc = document;
            _roof = roof;
            _view = view;
            _closeAction = closeAction;

            RunCommand = new RelayCommand(Run);
            CloseCommand = new RelayCommand(_closeAction);

            CollectLineBasedDetailItems();
        }

        // ------------------------------------------------------------
        // Detail Item detection
        // ------------------------------------------------------------

        private void CollectLineBasedDetailItems()
        {
            Log("Scanning for line-based Detail Item families…");

            var svc = new LineBasedDetailItemCollectorService();
            var symbols = svc.Collect(_doc, _view);

            Log($"Line-based Detail Item families found: {symbols.Count}");

            foreach (FamilySymbol symbol in symbols)
            {
                DetailItemSymbols.Add(new DetailItemSymbolInfo(symbol));
                Log($"  - {symbol.Family.Name} : {symbol.Name}");
            }

            if (DetailItemSymbols.Count > 0)
                SelectedDetailItemSymbol = DetailItemSymbols[0];
        }

        // ------------------------------------------------------------
        // Main workflow
        // ------------------------------------------------------------

        private void Run()
        {
            _log.Clear();
            RaisePropertyChanged(nameof(LogText));

            Log("Creaser started.");

            double radiusMm = double.Parse(DrainRadiusMm);
            double toleranceMm = double.Parse(ToleranceMm);

            double radiusInt =
                UnitUtils.ConvertToInternalUnits(radiusMm, UnitTypeId.Millimeters);

            double tolInt =
                UnitUtils.ConvertToInternalUnits(toleranceMm, UnitTypeId.Millimeters);

            var cornerSvc = new CornerCollectorService();
            var drainSvc = new DrainDetectionService();
            var creaseSvc = new CreaseFindingService();
            var pathSvc = new ShortestPathService();
            var dupSvc = new DuplicateLineRemovalService();
            var placeSvc = new DetailItemPlacementService();

            var shapePoints = ShapeEditingPointExtractor.GetShapePoints(_roof);
            Log($"Shape points scanned: {shapePoints.Count}");

            var corners = cornerSvc.Collect(_roof);
            Log($"Boundary corners: {corners.Count}");

            var drains = drainSvc.Detect(
                shapePoints,
                tolInt,
                radiusInt,
                out double lowestZ,
                out int rawCandidates,
                out int clusterCount);

            Log($"Lowest Z: {lowestZ}");
            Log($"Raw drain candidates: {rawCandidates}");
            Log($"Drain clusters: {clusterCount}");
            Log($"Final drains: {drains.Count}");

            // ✅ CORRECT VARIABLE NAME
            var graph = creaseSvc.BuildCreaseGraph(corners, drains);

            var paths = pathSvc.BuildPaths(
                graph,          // ✅ graph (NOT graphDict)
                corners,
                drains,
                out int failedPaths,
                (index, corner, drain, segmentCount) =>
                {
                    Log($"Crease {index}:");
                    Log($"  Corner: ({corner.X:F3}, {corner.Y:F3}, {corner.Z:F3})");
                    Log($"  Drain:  ({drain.X:F3}, {drain.Y:F3}, {drain.Z:F3})");
                    Log($"  Line segments: {segmentCount}");
                });

            Log($"Valid crease paths: {paths.Count - failedPaths}");
            Log($"Failed crease paths: {failedPaths}");

            var finalLines = dupSvc.RemoveDuplicates(
                paths,
                MergeCollinearCreases,
                out int dupRemoved);

            Log($"Duplicate lines removed: {dupRemoved}");

            int placed = placeSvc.Place(
                _doc,
                _view,
                finalLines,
                SelectedDetailItemSymbol?.Symbol);

            Log($"Final elements placed: {placed}");
            Log("Creaser finished.");
        }

        // ------------------------------------------------------------
        // Logging helper
        // ------------------------------------------------------------

        private void Log(string message)
        {
            _log.AppendLine(message);
            RaisePropertyChanged(nameof(LogText));
        }
    }

    // ------------------------------------------------------------
    // Simple ICommand implementation
    // ------------------------------------------------------------

    internal class RelayCommand : ICommand
    {
        private readonly Action _execute;

        public RelayCommand(Action execute)
        {
            _execute = execute;
        }

        public bool CanExecute(object parameter) => true;

        public void Execute(object parameter) => _execute();

        public event EventHandler CanExecuteChanged;
    }
}
