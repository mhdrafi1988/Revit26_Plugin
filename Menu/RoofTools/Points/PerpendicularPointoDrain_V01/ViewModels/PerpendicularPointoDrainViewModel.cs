using Autodesk.Revit.DB;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Revit26_Plugin.PDCV3.Models;
using Revit26_Plugin.PDCV3.Services;
using Revit26_Plugin.PerpendicularPointoDrain.V01.Models;
using Revit26_Plugin.PerpendicularPointoDrain.V01.Services;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace Revit26_Plugin.PerpendicularPointoDrain.V01.ViewModels
{
    public class PerpendicularPointoDrainViewModel : ObservableObject
    {
        private readonly Document _doc;
        private readonly RoofBase _roof;

        // Loop extraction is reused directly from PDCV3 — same assembly, no duplication needed.
        private readonly RoofGeometryService       _geometryService   = new RoofGeometryService();
        private readonly DrainGroupingService      _groupingService   = new DrainGroupingService();
        private readonly BoundaryProjectionService _projectionService = new BoundaryProjectionService();

        // Points picked up front in the command, before this ViewModel/window ever existed.
        private readonly List<XYZ> _rawDrainPoints;

        private List<DrainGroupModel>   _drainGroups    = new List<DrainGroupModel>();
        private List<LoopBoundaryModel> _loopBoundaries = new List<LoopBoundaryModel>();

        // ── Drain summary ─────────────────────────────────────────────────────────
        private string _drainSummary = string.Empty;
        public string DrainSummary
        {
            get => _drainSummary;
            set => SetProperty(ref _drainSummary, value);
        }

        // ── Tolerance ─────────────────────────────────────────────────────────────
        private double _toleranceMm = 500;
        public double ToleranceMm
        {
            get => _toleranceMm;
            set => SetProperty(ref _toleranceMm, value);
        }

        // ── Direction toggles (Project North = +Y) ───────────────────────────────
        private bool _isNorthEnabled = true;
        public bool IsNorthEnabled { get => _isNorthEnabled; set => SetProperty(ref _isNorthEnabled, value); }

        private bool _isNortheastEnabled = true;
        public bool IsNortheastEnabled { get => _isNortheastEnabled; set => SetProperty(ref _isNortheastEnabled, value); }

        private bool _isEastEnabled = true;
        public bool IsEastEnabled { get => _isEastEnabled; set => SetProperty(ref _isEastEnabled, value); }

        private bool _isSoutheastEnabled = true;
        public bool IsSoutheastEnabled { get => _isSoutheastEnabled; set => SetProperty(ref _isSoutheastEnabled, value); }

        private bool _isSouthEnabled = true;
        public bool IsSouthEnabled { get => _isSouthEnabled; set => SetProperty(ref _isSouthEnabled, value); }

        private bool _isSouthwestEnabled = true;
        public bool IsSouthwestEnabled { get => _isSouthwestEnabled; set => SetProperty(ref _isSouthwestEnabled, value); }

        private bool _isWestEnabled = true;
        public bool IsWestEnabled { get => _isWestEnabled; set => SetProperty(ref _isWestEnabled, value); }

        private bool _isNorthwestEnabled = true;
        public bool IsNorthwestEnabled { get => _isNorthwestEnabled; set => SetProperty(ref _isNorthwestEnabled, value); }

        // ── Loop / vertex options ─────────────────────────────────────────────────
        private bool _includeInteriorLoops = true;
        public bool IncludeInteriorLoops { get => _includeInteriorLoops; set => SetProperty(ref _includeInteriorLoops, value); }

        private bool _snapToVertex = true;
        public bool SnapToVertex { get => _snapToVertex; set => SetProperty(ref _snapToVertex, value); }

        // ── Results / log ─────────────────────────────────────────────────────────
        public ObservableCollection<ProjectionResultModel> Results { get; } = new ObservableCollection<ProjectionResultModel>();

        private string _logText = string.Empty;
        public string LogText
        {
            get => _logText;
            set => SetProperty(ref _logText, value);
        }

        // ── Commands ──────────────────────────────────────────────────────────────
        public IRelayCommand AnalyzeCommand { get; }
        public IRelayCommand ApplyCommand    { get; }

        // ─────────────────────────────────────────────────────────────────────────
        public PerpendicularPointoDrainViewModel(Document doc, RoofBase roof, List<XYZ> drainPoints)
        {
            _doc            = doc;
            _roof           = roof;
            _rawDrainPoints = drainPoints ?? new List<XYZ>();

            AnalyzeCommand = new RelayCommand(Analyze);
            ApplyCommand    = new RelayCommand(Apply);

            RegroupDrains();
            AppendLog($"✅ Captured {_rawDrainPoints.Count} drain point(s), grouped into {_drainGroups.Count} centroid group(s) at {ToleranceMm}mm tolerance.");
        }

        // ── Grouping ──────────────────────────────────────────────────────────────
        // Re-runs from the original picked points using whatever's currently in the
        // Tolerance field — called once at construction and again at the start of every
        // Analyze, so changing tolerance and re-analyzing re-groups freshly each time.
        private void RegroupDrains()
        {
            double toleranceFeet = UnitUtils.ConvertToInternalUnits(ToleranceMm, UnitTypeId.Millimeters);
            _drainGroups = _groupingService.GroupDrains(_rawDrainPoints, toleranceFeet);
            DrainSummary = $"{_rawDrainPoints.Count} drain(s) → {_drainGroups.Count} group(s)";
        }

        // ── Analyze (dry run) ─────────────────────────────────────────────────────
        private void Analyze()
        {
            Results.Clear();

            if (!_rawDrainPoints.Any())
            {
                AppendLog("⚠️ Analyze skipped — no drain points were captured.");
                return;
            }

            RegroupDrains();
            AppendLog($"── Re-grouped {_rawDrainPoints.Count} drain point(s) into {_drainGroups.Count} group(s) at {ToleranceMm}mm tolerance. ──");

            List<RoofLoopModel> rawLoops;
            try
            {
                rawLoops = _geometryService.ExtractLoops(_roof);
            }
            catch (Exception ex)
            {
                AppendLog($"❌ Geometry extraction failed: {ex.Message}");
                return;
            }

            _loopBoundaries = new List<LoopBoundaryModel>();
            int innerIdx = 0;
            foreach (var l in rawLoops)
            {
                if (l.LoopType == "Outer")
                {
                    _loopBoundaries.Add(new LoopBoundaryModel { Label = "Outer", Curves = l.Geometry.ToList() });
                }
                else if (IncludeInteriorLoops)
                {
                    innerIdx++;
                    _loopBoundaries.Add(new LoopBoundaryModel { Label = $"Inner #{innerIdx}", Curves = l.Geometry.ToList() });
                }
            }

            if (!_loopBoundaries.Any())
            {
                AppendLog("⚠️ No boundary loops found on the selected roof.");
                return;
            }

            var enabledDirections = GetEnabledDirections();
            if (!enabledDirections.Any())
            {
                AppendLog("⚠️ No directions enabled — nothing to search.");
                return;
            }

            foreach (var group in _drainGroups)
            {
                var candidates = _projectionService.ComputeCandidates(group, _loopBoundaries, enabledDirections);
                foreach (var c in candidates) Results.Add(c);
            }

            int fallbackCount = Results.Count(r => r.IsFallback);
            AppendLog($"✅ Analysis complete — {Results.Count} candidate point(s) across {_drainGroups.Count} group(s) and {_loopBoundaries.Count} loop(s). {fallbackCount} used the nearest-edge fallback.");
        }

        // ── Apply (commit) ────────────────────────────────────────────────────────
        private void Apply()
        {
            if (!Results.Any())
            {
                AppendLog("⚠️ Apply skipped — run Analyze first.");
                return;
            }

            AppendLog("── Applying points... ──");

            using (Transaction tx = new Transaction(_doc, "Add Perpendicular Drain Points"))
            {
                tx.Start();

                SlabShapeEditor editor = _roof.GetSlabShapeEditor();
                if (!editor.IsEnabled)
                    editor.Enable();

                double snapToleranceFeet = UnitUtils.ConvertToInternalUnits(ToleranceMm, UnitTypeId.Millimeters);
                _projectionService.ApplyPoints(editor, Results, SnapToVertex, snapToleranceFeet);

                foreach (var r in Results)
                {
                    if (r.Point != null && r.Status != null && r.Status.StartsWith("Added"))
                        PointMarkerService.CreateCircleMarker(_doc, r.Point, 25);
                }

                tx.Commit();
            }

            int added   = Results.Count(r => r.Status.StartsWith("Added"));
            int skipped = Results.Count(r => r.Status.StartsWith("Skipped"));
            AppendLog($"── Done. {added} point(s) added, {skipped} skipped. ──");
        }

        // ── Helpers ───────────────────────────────────────────────────────────────
        private List<string> GetEnabledDirections()
        {
            var dirs = new List<string>();
            if (IsNorthEnabled)     dirs.Add("N");
            if (IsNortheastEnabled) dirs.Add("NE");
            if (IsEastEnabled)      dirs.Add("E");
            if (IsSoutheastEnabled) dirs.Add("SE");
            if (IsSouthEnabled)     dirs.Add("S");
            if (IsSouthwestEnabled) dirs.Add("SW");
            if (IsWestEnabled)      dirs.Add("W");
            if (IsNorthwestEnabled) dirs.Add("NW");
            return dirs;
        }

        private void AppendLog(string message)
        {
            string timestamp = DateTime.Now.ToString("HH:mm:ss");
            LogText += $"[{timestamp}] {message}{Environment.NewLine}";
        }
    }
}
