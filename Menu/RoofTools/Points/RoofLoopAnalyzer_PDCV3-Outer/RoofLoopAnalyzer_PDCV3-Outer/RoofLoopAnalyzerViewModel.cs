using Autodesk.Revit.DB;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Revit26_Plugin.PDCV3.Models;
using Revit26_Plugin.PDCV3.Services;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace Revit26_Plugin.PDCV3.ViewModels
{
    public class RoofLoopAnalyzerViewModel : ObservableObject
    {
        private readonly Document            _doc;
        private readonly RoofBase            _roof;
        private readonly RoofGeometryService _geometryService;
        private readonly LoopDivisionService _divisionService;

        // ── Loops DataGrid ────────────────────────────────────────────────────────
        public ObservableCollection<RoofLoopModel> Loops { get; set; }

        // ── Log area ──────────────────────────────────────────────────────────────
        private string _logText = string.Empty;
        public string LogText
        {
            get => _logText;
            set => SetProperty(ref _logText, value);
        }

        // ── Density dropdown (pts per metre) ─────────────────────────────────────
        // Available densities: 1 through 10 pts/m
        public List<int> DensityOptions { get; } = Enumerable.Range(1, 10).ToList();

        private int _selectedDensity = 3;   // default: 3 pts/m
        public int SelectedDensity
        {
            get => _selectedDensity;
            set
            {
                if (SetProperty(ref _selectedDensity, value))
                    RecalculateAllPoints();
            }
        }

        // ── Commands ──────────────────────────────────────────────────────────────
        public IRelayCommand AnalyzeCommand       { get; }
        public IRelayCommand ApplyDivisionCommand { get; }

        // ─────────────────────────────────────────────────────────────────────────
        public RoofLoopAnalyzerViewModel(Document doc, RoofBase roof)
        {
            _doc             = doc;
            _roof            = roof;
            _geometryService = new RoofGeometryService();
            _divisionService = new LoopDivisionService();
            Loops            = new ObservableCollection<RoofLoopModel>();

            AnalyzeCommand       = new RelayCommand(AnalyzeRoof);
            ApplyDivisionCommand = new RelayCommand(ApplyDivisions);
        }

        // ── Analyze ───────────────────────────────────────────────────────────────
        private void AnalyzeRoof()
        {
            Loops.Clear();
            AppendLog("── Analyzing roof geometry... ──");

            List<RoofLoopModel> allLoops;
            try
            {
                allLoops = _geometryService.ExtractLoops(_roof);
            }
            catch (Exception ex)
            {
                AppendLog($"❌ Geometry extraction failed: {ex.Message}");
                return;
            }

            // Show only OUTER loops that have curved segments
            var targetLoops = allLoops
                .Where(l => l.LoopType == "Outer" && l.HasCurvedSegments)
                .ToList();

            foreach (var loop in targetLoops)
            {
                loop.RecommendedPoints = ComputePoints(loop);
                loop.IsSelected        = true;
                Loops.Add(loop);
            }

            int total     = Loops.Count;
            int circular  = Loops.Count(l => l.LoopShapeType == "Circular");
            int oval      = Loops.Count(l => l.LoopShapeType == "Oval");
            int arc       = Loops.Count(l => l.LoopShapeType == "Arc");
            int other     = Loops.Count(l => l.LoopShapeType == "Other");

            AppendLog($"✅ Analysis complete — Outer curved loops: {total}");
            AppendLog($"   Circular: {circular}  |  Oval: {oval}  |  Arc: {arc}  |  Other: {other}");

            if (total == 0)
                AppendLog("⚠️ No outer loops with curved segments found on the selected roof.");
        }

        // ── Apply divisions ───────────────────────────────────────────────────────
        private void ApplyDivisions()
        {
            var validLoops = Loops.Where(l => l.IsSelected && l.RecommendedPoints > 0).ToList();

            if (!validLoops.Any())
            {
                AppendLog("⚠️ Apply skipped — no loops are selected.");
                return;
            }

            AppendLog($"── Applying divisions at {_selectedDensity} pt/m to {validLoops.Count} loop(s)... ──");

            List<string> serviceLog = _divisionService.AddDivisionPoints(_doc, _roof, validLoops);

            foreach (var entry in serviceLog)
                AppendLog(entry);

            int totalRequested = validLoops.Sum(l => l.RecommendedPoints);
            AppendLog($"── Done. {totalRequested} point(s) requested across {validLoops.Count} loop(s). ──");
        }

        // ── Density → point count ─────────────────────────────────────────────────
        /// <summary>
        /// Points = density (pts/m) × perimeter (m), minimum 4.
        /// </summary>
        private int ComputePoints(RoofLoopModel loop)
        {
            double perimeterM = loop.PerimeterMm / 1000.0;
            return Math.Max(4, (int)Math.Round(_selectedDensity * perimeterM));
        }

        private void RecalculateAllPoints()
        {
            foreach (var loop in Loops)
                loop.RecommendedPoints = ComputePoints(loop);
        }

        // ── Log helper ────────────────────────────────────────────────────────────
        private void AppendLog(string message)
        {
            string timestamp = DateTime.Now.ToString("HH:mm:ss");
            LogText += $"[{timestamp}] {message}{Environment.NewLine}";
        }
    }
}
