using Autodesk.Revit.DB;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Revit26_Plugin.PonitOnCurvesInnerandOuter.V01.Models;
using Revit26_Plugin.PonitOnCurvesInnerandOuter.V01.Services;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Windows.Data;

namespace Revit26_Plugin.PonitOnCurvesInnerandOuter.V01.ViewModels
{
    public class RoofLoopAnalyzerViewModel : ObservableObject
    {
        private readonly Document _doc;
        private readonly RoofBase _roof;
        private readonly RoofGeometryService _geometryService;
        private readonly LoopDivisionService _divisionService;

        // ── Raw collection ────────────────────────────────────────────────────────
        public ObservableCollection<RoofLoopModel> Loops { get; } = new ObservableCollection<RoofLoopModel>();

        // ── Grouped view (bound to DataGrid) ─────────────────────────────────────
        public ICollectionView GroupedLoops { get; private set; }

        // ── Log (StringBuilder + line cap to prevent OOM on large roofs) ─────────
        private readonly System.Text.StringBuilder _logBuilder = new System.Text.StringBuilder();
        private readonly System.Collections.Generic.Queue<string> _logLines
            = new System.Collections.Generic.Queue<string>();
        private const int LogLineCap = 500;

        private string _logText = string.Empty;
        public string LogText
        {
            get => _logText;
            set => SetProperty(ref _logText, value);
        }

        // ── Summary ───────────────────────────────────────────────────────────────
        private string _summaryText = string.Empty;
        public string SummaryText
        {
            get => _summaryText;
            set => SetProperty(ref _summaryText, value);
        }

        // ── Commands ──────────────────────────────────────────────────────────────
        public IRelayCommand AnalyzeCommand { get; }
        public IRelayCommand ApplyDivisionCommand { get; }
        public IRelayCommand<string> SelectAllInGroupCommand { get; }
        public IRelayCommand<string> SelectNoneInGroupCommand { get; }

        // ─────────────────────────────────────────────────────────────────────────
        public RoofLoopAnalyzerViewModel(Document doc, RoofBase roof)
        {
            _doc = doc;
            _roof = roof;
            _geometryService = new RoofGeometryService();
            _divisionService = new LoopDivisionService();

            AnalyzeCommand = new RelayCommand(AnalyzeRoof);
            ApplyDivisionCommand = new RelayCommand(ApplyDivisions);
            SelectAllInGroupCommand = new RelayCommand<string>(g => SetGroupSelection(g, true));
            SelectNoneInGroupCommand = new RelayCommand<string>(g => SetGroupSelection(g, false));

            BuildGroupedView();
        }

        // ─────────────────────────────────────────────────────────────────────────
        // Grouped CollectionView setup
        // ─────────────────────────────────────────────────────────────────────────
        private void BuildGroupedView()
        {
            var view = CollectionViewSource.GetDefaultView(Loops);
            view.GroupDescriptions.Clear();

            // Primary group: LoopType (Outer / Inner)
            view.GroupDescriptions.Add(new PropertyGroupDescription(nameof(RoofLoopModel.LoopType)));
            // Secondary group: LoopShapeType (Circular, Oval, Rectangle, Other)
            view.GroupDescriptions.Add(new PropertyGroupDescription(nameof(RoofLoopModel.LoopShapeType)));

            // Sort: LoopType (Outer first), then ShapeType, then Index
            view.SortDescriptions.Clear();
            view.SortDescriptions.Add(new SortDescription(nameof(RoofLoopModel.LoopType), ListSortDirection.Descending)); // "Outer" > "Inner"
            view.SortDescriptions.Add(new SortDescription(nameof(RoofLoopModel.LoopShapeType), ListSortDirection.Ascending));
            view.SortDescriptions.Add(new SortDescription(nameof(RoofLoopModel.Index), ListSortDirection.Ascending));

            GroupedLoops = view;
        }

        // ─────────────────────────────────────────────────────────────────────────
        // Analyze
        // ─────────────────────────────────────────────────────────────────────────
        private void AnalyzeRoof()
        {
            Loops.Clear();
            AppendLog("── Analyzing roof geometry (Inner + Outer)... ──");

            List<RoofLoopModel> extracted;
            try
            {
                extracted = _geometryService.ExtractLoops(_roof);
            }
            catch (Exception ex)
            {
                AppendLog($"❌ Geometry extraction failed: {ex.Message}");
                return;
            }

            foreach (var loop in extracted)
            {
                loop.IsSelected = true;
                Loops.Add(loop);
            }

            int total = Loops.Count;
            int outer = Loops.Count(l => l.LoopType == "Outer");
            int inner = Loops.Count(l => l.LoopType == "Inner");
            int circular = Loops.Count(l => l.LoopShapeType == "Circular");
            int oval = Loops.Count(l => l.LoopShapeType == "Oval");
            int arc = Loops.Count(l => l.LoopShapeType == "Arc");
            int rect = Loops.Count(l => l.LoopShapeType == "Rectangle");
            int other = Loops.Count(l => l.LoopShapeType == "Other");

            SummaryText = $"Total: {total}  |  Outer: {outer}  Inner: {inner}  |  Circular: {circular}  Oval: {oval}  Arc: {arc}  Rect: {rect}  Other: {other}";

            AppendLog($"✅ Done — {total} loop(s) found.");
            AppendLog($"   Outer: {outer}  Inner: {inner}");
            AppendLog($"   Circular: {circular}  Oval: {oval}  Arc: {arc}  Rectangle: {rect}  Other: {other}");

            if (total == 0)
                AppendLog("⚠️ No loops found on the selected roof.");
        }

        // ─────────────────────────────────────────────────────────────────────────
        // Apply
        // ─────────────────────────────────────────────────────────────────────────
        private void ApplyDivisions()
        {
            var selected = Loops.Where(l => l.IsSelected && l.RecommendedPoints > 0).ToList();

            if (!selected.Any())
            {
                AppendLog("⚠️ Apply skipped — no loops selected.");
                return;
            }

            AppendLog($"── Applying to {selected.Count} loop(s)... ──");

            var serviceLog = _divisionService.AddDivisionPoints(_doc, _roof, selected);
            foreach (var entry in serviceLog)
                AppendLog(entry);

            int totalPts = selected.Sum(l => l.RecommendedPoints);
            AppendLog($"── Done. {totalPts} point(s) requested across {selected.Count} loop(s). ──");
        }

        // ─────────────────────────────────────────────────────────────────────────
        // Per-group select/deselect
        // GroupKey format: "LoopType — LoopShapeType"  OR just a LoopType string
        // We match against the RoofLoopModel.GroupKey property.
        // ─────────────────────────────────────────────────────────────────────────
        private void SetGroupSelection(string groupKey, bool value)
        {
            if (string.IsNullOrEmpty(groupKey)) return;

            // groupKey may be a top-level group ("Outer"/"Inner") or a leaf group key
            foreach (var loop in Loops)
            {
                bool matchesTop = loop.LoopType == groupKey;
                bool matchesLeaf = loop.GroupKey == groupKey;
                bool matchesShape = loop.LoopShapeType == groupKey;

                if (matchesTop || matchesLeaf || matchesShape)
                    loop.IsSelected = value;
            }
        }

        // ─────────────────────────────────────────────────────────────────────────
        // Log
        // ─────────────────────────────────────────────────────────────────────────
        private void AppendLog(string msg)
        {
            string line = $"[{DateTime.Now:HH:mm:ss}] {msg}";

            _logLines.Enqueue(line);

            // Evict oldest lines when over cap
            while (_logLines.Count > LogLineCap)
                _logLines.Dequeue();

            _logBuilder.Clear();
            foreach (var l in _logLines)
                _logBuilder.AppendLine(l);

            LogText = _logBuilder.ToString();
        }
    }
}