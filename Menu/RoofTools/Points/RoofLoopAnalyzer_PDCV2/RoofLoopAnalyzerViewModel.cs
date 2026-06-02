using Autodesk.Revit.DB;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Revit26_Plugin.PDCV2.Models;
using Revit26_Plugin.PDCV2.Services;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace Revit26_Plugin.PDCV2.ViewModels
{
    public class RoofLoopAnalyzerViewModel : ObservableObject
    {
        private readonly Document           _doc;
        private readonly RoofBase           _roof;
        private readonly RoofGeometryService _geometryService;
        private readonly LoopDivisionService _divisionService;

        // ── Loops DataGrid ───────────────────────────────────────────────────────
        public ObservableCollection<RoofLoopModel> Loops { get; set; }

        // ── Log area (running history) ────────────────────────────────────────────
        private string _logText = string.Empty;
        public string LogText
        {
            get => _logText;
            set => SetProperty(ref _logText, value);
        }

        // ── Global point-count dropdown ──────────────────────────────────────────
        public List<int> PointCountOptions { get; } = new List<int> { 4, 6, 8, 10, 12, 14, 16 };

        private int _selectedPointCount = 8;
        public int SelectedPointCount
        {
            get => _selectedPointCount;
            set
            {
                if (SetProperty(ref _selectedPointCount, value))
                {
                    // Keep all loop models in sync with the global selection
                    foreach (var loop in Loops)
                        loop.RecommendedPoints = value;
                }
            }
        }

        // ── Commands ─────────────────────────────────────────────────────────────
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

            List<RoofLoopModel> loops;
            try
            {
                loops = _geometryService.ExtractCircularLoops(_roof);
            }
            catch (Exception ex)
            {
                AppendLog($"❌ Geometry extraction failed: {ex.Message}");
                return;
            }

            var innerLoops = loops.Where(l => l.LoopType == "Inner").ToList();

            foreach (var loop in innerLoops)
            {
                loop.RecommendedPoints = _selectedPointCount;
                loop.IsSelected        = true;
                Loops.Add(loop);
            }

            int total      = Loops.Count;
            int circular   = Loops.Count(l => l.LoopShapeType == "Circular");
            int rectangles = Loops.Count(l => l.LoopShapeType == "Rectangle");
            int others     = Loops.Count(l => l.LoopShapeType == "Other");

            AppendLog($"✅ Analysis complete — Inner Loops: {total}");
            AppendLog($"   Circular: {circular}  |  Rectangle: {rectangles}  |  Other: {others}");

            if (total == 0)
                AppendLog("⚠️ No inner loops found on the selected roof.");
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

            AppendLog($"── Applying {_selectedPointCount} division points to {validLoops.Count} loop(s)... ──");

            List<string> serviceLog = _divisionService.AddDivisionPoints(_doc, _roof, validLoops);

            foreach (var entry in serviceLog)
                AppendLog(entry);

            int totalAdded = validLoops.Sum(l => l.RecommendedPoints);
            AppendLog($"── Done. Requested {totalAdded} point(s) across {validLoops.Count} loop(s). ──");
        }

        // ── Log helper ────────────────────────────────────────────────────────────
        private void AppendLog(string message)
        {
            string timestamp = DateTime.Now.ToString("HH:mm:ss");
            LogText += $"[{timestamp}] {message}{Environment.NewLine}";
        }
    }
}
