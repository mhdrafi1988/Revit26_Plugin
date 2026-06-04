using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;
using Revit26_Plugin.RoofTools.LineAndPoints.RoofRidgeLines_V51.Models;
using Revit26_Plugin.RoofTools.LineAndPoints.RoofRidgeLines_V51.Services;

namespace Revit26_Plugin.RoofTools.LineAndPoints.RoofRidgeLines_V51.ViewModels
{
    public partial class RoofRidgeViewModel : ObservableObject
    {
        // ── Services ──────────────────────────────────────────────────────────────
        private readonly UIDocument _uiDoc;
        private readonly Document _doc;
        private readonly DrainGroupingService _groupingSvc;
        private readonly VoronoiComputationService _voronoiSvc;
        private readonly RoofBoundaryService _boundarySvc;
        private readonly VoronoiClippingService _clippingSvc;
        private readonly RidgeValidationService _validationSvc;
        private readonly RidgeCreationService _creationSvc;

        // ── Internal state ────────────────────────────────────────────────────────
        private RoofBase _selectedRoof;
        private List<XYZ> _selectedDrainLocations = new List<XYZ>();
        private VoronoiRidgeResult _lastResult;
        private Window _ownerWindow;

        // ── Full pipeline log (every stage, copyable) ─────────────────────────────
        // PipelineLog drives the flat PipelineLogText string bound to the read-only
        // TextBox in the Pipeline Log tab — making Ctrl+A / Ctrl+C work natively.
        public ObservableCollection<string> PipelineLog { get; }
            = new ObservableCollection<string>();

        /// <summary>
        /// Flat newline-joined text of PipelineLog.
        /// Bound (OneWay) to the read-only TextBox in the Pipeline Log tab.
        /// Ctrl+A selects all; Ctrl+C copies the selection.
        /// </summary>
        [ObservableProperty]
        private string _pipelineLogText = string.Empty;

        // ── Constructors ──────────────────────────────────────────────────────────

        public RoofRidgeViewModel(UIDocument uiDoc, RoofBase roof, List<XYZ> drainLocations)
            : this(uiDoc)
        {
            _selectedRoof = roof ?? throw new ArgumentNullException(nameof(roof));
            _selectedDrainLocations = drainLocations ?? new List<XYZ>();

            RoofDescription = $"Roof: {_selectedRoof.Name}  (Id {_selectedRoof.Id})";
            DrainCount = _selectedDrainLocations.Count;
            StatusMessage = $"Pre-selected {DrainCount} drain point(s). You may re-select if needed.";
        }

        public RoofRidgeViewModel(UIDocument uiDoc)
        {
            _uiDoc = uiDoc ?? throw new ArgumentNullException(nameof(uiDoc));
            _doc = uiDoc.Document;
            _groupingSvc = new DrainGroupingService();
            _voronoiSvc = new VoronoiComputationService();
            _boundarySvc = new RoofBoundaryService();
            _clippingSvc = new VoronoiClippingService();
            _validationSvc = new RidgeValidationService();
            _creationSvc = new RidgeCreationService();
        }

        public void SetOwnerWindow(Window window) => _ownerWindow = window;

        // ── Observable properties ─────────────────────────────────────────────────

        [ObservableProperty]
        [NotifyCanExecuteChangedFor(nameof(RunCommand))]
        private string _roofDescription = "No roof selected";

        [ObservableProperty]
        [NotifyCanExecuteChangedFor(nameof(RunCommand))]
        private int _drainCount;

        [ObservableProperty]
        [NotifyCanExecuteChangedFor(nameof(RunCommand))]
        private double _proximityDistanceMm = 50.0;

        [ObservableProperty]
        private double _validationToleranceMm = 50.0;

        [ObservableProperty]
        private bool _isBusy;

        [ObservableProperty]
        private string _statusMessage = "Ready.";

        [ObservableProperty]
        private string _progressDetail = string.Empty;

        [ObservableProperty]
        private int _passCount;

        [ObservableProperty]
        private int _failCount;

        [ObservableProperty]
        private string _groupCountDisplay = "0";

        public ObservableCollection<ValidationEntry> ValidationLog { get; }
            = new ObservableCollection<ValidationEntry>();

        // ── Commands ──────────────────────────────────────────────────────────────

        [RelayCommand]
        private void SelectRoof()
        {
            try
            {
                _ownerWindow?.Hide();
                StatusMessage = "Click on a Roof element in the view…";
                var ref_ = _uiDoc.Selection.PickObject(
                    ObjectType.Element,
                    new RoofSelectionFilter(),
                    "Select a Roof");

                _selectedRoof = _doc.GetElement(ref_) as RoofBase;
                if (_selectedRoof == null)
                {
                    RoofDescription = "Selection failed — please retry";
                    StatusMessage = "Roof selection cancelled or invalid.";
                    return;
                }

                RoofDescription = $"Roof: {_selectedRoof.Name}  (Id {_selectedRoof.Id})";
                StatusMessage = "Roof selected. Now select drain points.";
            }
            catch (Autodesk.Revit.Exceptions.OperationCanceledException)
            {
                StatusMessage = "Roof selection cancelled.";
            }
            catch (Exception ex)
            {
                StatusMessage = $"Error selecting roof: {ex.Message}";
            }
            finally
            {
                _ownerWindow?.Show();
            }
        }

        [RelayCommand]
        private void SelectDrains()
        {
            if (_selectedRoof == null)
            {
                StatusMessage = "Please select a roof first.";
                return;
            }

            try
            {
                _ownerWindow?.Hide();
                var newDrains = DrainPointPicker.PickDrainPoints(_uiDoc, _selectedRoof);
                _selectedDrainLocations = newDrains;
                DrainCount = _selectedDrainLocations.Count;
                StatusMessage = $"{DrainCount} drain point(s) selected.";
            }
            catch (Autodesk.Revit.Exceptions.OperationCanceledException)
            {
                StatusMessage = "Drain selection cancelled.";
            }
            catch (Exception ex)
            {
                StatusMessage = $"Error during drain selection: {ex.Message}";
            }
            finally
            {
                _ownerWindow?.Show();
            }
        }

        // ── CopyLog: copies the full pipeline log ─────────────────────────────────

        [RelayCommand]
        private void CopyLog()
        {
            if (PipelineLog.Count == 0 && ValidationLog.Count == 0)
            {
                StatusMessage = "No log data to copy. Run the command first.";
                return;
            }

            var sb = new StringBuilder();

            // ── Section 1: Pipeline log ───────────────────────────────────────────
            sb.AppendLine("════════════════════════════════════════════════════════════");
            sb.AppendLine("  ROOF RIDGE LINES – PIPELINE LOG");
            sb.AppendLine($"  Copied: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            sb.AppendLine("════════════════════════════════════════════════════════════");
            foreach (var line in PipelineLog)
                sb.AppendLine(line);

            // ── Section 2: Validation table ───────────────────────────────────────
            if (ValidationLog.Count > 0)
            {
                sb.AppendLine();
                sb.AppendLine("════════════════════════════════════════════════════════════");
                sb.AppendLine("  VALIDATION TABLE  (tab-separated — paste into Excel)");
                sb.AppendLine("════════════════════════════════════════════════════════════");
                sb.AppendLine("Pt#\tX (ft)\tY (ft)\tX (mm)\tY (mm)\tMaxDev (ft)\tMaxDev (mm)\tTol (ft)\tPassed\tGroups\tNote");

                foreach (var e in ValidationLog)
                {
                    double xMm = e.X * 304.8;
                    double yMm = e.Y * 304.8;
                    double devMm = e.MaxDeviation * 304.8;
                    double tolMm = e.Tolerance * 304.8;
                    string groups = string.Join("+", e.DistancesToGroups.Select(
                        kv => $"G{kv.Key}={kv.Value * 304.8:F1}mm"));
                    sb.AppendLine(
                        $"{e.PointIndex}\t{e.X:F4}\t{e.Y:F4}\t{xMm:F1}\t{yMm:F1}" +
                        $"\t{e.MaxDeviation:F6}\t{devMm:F2}\t{tolMm:F2}" +
                        $"\t{(e.Passed ? "PASS" : "FAIL")}\t{groups}\t{e.Note}");
                }

                sb.AppendLine();
                sb.AppendLine($"SUMMARY\tPass: {PassCount}\tFail: {FailCount}" +
                              $"\tTotal: {ValidationLog.Count}");
            }

            try
            {
                Clipboard.SetText(sb.ToString());
                StatusMessage = $"Full log copied to clipboard ({PipelineLog.Count} log lines, {ValidationLog.Count} validation rows).";
            }
            catch (Exception ex)
            {
                StatusMessage = $"Copy failed: {ex.Message}";
            }
        }

        // ── Run ───────────────────────────────────────────────────────────────────

        [RelayCommand(CanExecute = nameof(CanRun))]
        private async Task Run()
        {
            IsBusy = true;
            ValidationLog.Clear();
            PipelineLog.Clear();
            PipelineLogText = string.Empty;
            StatusMessage = "Running Voronoi ridge generation…";

            var runStart = DateTime.Now;
            Log($"[START]  {runStart:HH:mm:ss}");
            Log($"[INPUT]  Roof: {_selectedRoof?.Name}  Id={_selectedRoof?.Id}");
            Log($"[INPUT]  Raw drain points: {_selectedDrainLocations.Count}");
            Log($"[INPUT]  Proximity distance: {ProximityDistanceMm} mm  ({DrainGroupingService.MmToFeet(ProximityDistanceMm):F5} ft)");
            Log($"[INPUT]  Validation tolerance: {ValidationToleranceMm} mm");

            // Log every raw drain location
            for (int i = 0; i < _selectedDrainLocations.Count; i++)
            {
                var p = _selectedDrainLocations[i];
                Log($"  Drain[{i}]  X={p.X:F4} ft ({p.X * 304.8:F1} mm)  " +
                    $"Y={p.Y:F4} ft ({p.Y * 304.8:F1} mm)  Z={p.Z:F4} ft");
            }

            try
            {
                // ── Step 1: Drain grouping ────────────────────────────────────────
                UpdateProgress("Grouping drains by proximity…");
                Log("[STEP 1] Grouping drains by proximity…");

                double proxFeet = DrainGroupingService.MmToFeet(ProximityDistanceMm);
                var drainGroups = _groupingSvc.GroupDrains(_selectedDrainLocations, proxFeet);
                int groupCount = drainGroups.Count;
                GroupCountDisplay = groupCount.ToString();

                Log($"  Groups formed: {groupCount}");
                foreach (var g in drainGroups)
                {
                    Log($"  Group[{g.GroupIndex}]  Drains={g.DrainLocations.Count}" +
                        $"  Centroid X={g.Centroid.X:F4} ft ({g.Centroid.X * 304.8:F1} mm)" +
                        $"  Y={g.Centroid.Y:F4} ft ({g.Centroid.Y * 304.8:F1} mm)");
                    for (int di = 0; di < g.DrainLocations.Count; di++)
                    {
                        var dp = g.DrainLocations[di];
                        Log($"    Drain[{di}]  X={dp.X:F4} ft  Y={dp.Y:F4} ft");
                    }
                }

                if (groupCount < 2)
                    throw new InvalidOperationException(
                        "At least two distinct drain groups are required. " +
                        "Reduce the proximity distance or add more drain points.");

                if (groupCount < 3)
                    Log($"  [WARN] Only {groupCount} groups — midline bisector mode will be used.");

                // ── Step 2–5: Background heavy work ──────────────────────────────
                var result = new VoronoiRidgeResult();
                result.DrainGroups = drainGroups;

                List<XYZ> boundary = null;

                await Task.Run(() =>
                {
                    // Step 2: Boundary
                    Dispatch(() => { UpdateProgress("Extracting roof boundary…"); Log("[STEP 2] Extracting roof boundary…"); });
                    boundary = _boundarySvc.ExtractBoundary(_selectedRoof);
                    Dispatch(() =>
                    {
                        Log($"  Boundary vertices: {boundary.Count}");
                        for (int vi = 0; vi < boundary.Count; vi++)
                        {
                            var v = boundary[vi];
                            Log($"  V[{vi}]  X={v.X:F4} ft ({v.X * 304.8:F1} mm)  Y={v.Y:F4} ft ({v.Y * 304.8:F1} mm)");
                        }

                        // Approximate bounding box
                        double minX = boundary.Min(p => p.X), maxX = boundary.Max(p => p.X);
                        double minY = boundary.Min(p => p.Y), maxY = boundary.Max(p => p.Y);
                        Log($"  Boundary BBox  X=[{minX * 304.8:F1}, {maxX * 304.8:F1}] mm" +
                            $"  Y=[{minY * 304.8:F1}, {maxY * 304.8:F1}] mm");
                    });

                    // Step 3: Voronoi
                    Dispatch(() => { UpdateProgress("Computing Voronoi diagram…"); Log("[STEP 3] Computing Voronoi diagram…"); });
                    _voronoiSvc.Compute(result.DrainGroups, result);
                    Dispatch(() =>
                    {
                        // ── Detect which branch fired ─────────────────────────────
                        int gc = result.DrainGroups.Count;
                        string branch;
                        if (gc == 2)
                            branch = "2-GROUP MIDLINE BISECTOR";
                        else if (result.RawVoronoiVertices.Count == 1
                                 && result.RawVoronoiEdges.Count > 0
                                 && result.RawVoronoiEdges.All(e =>
                                     Math.Abs(e.Start.X - result.RawVoronoiVertices[0].X) < 1e-4 &&
                                     Math.Abs(e.Start.Y - result.RawVoronoiVertices[0].Y) < 1e-4))
                            branch = "CONVEX-HULL BISECTOR RAYS  [Case 5 – degenerate/rectangle]";
                        else if (result.RawVoronoiEdges.Count == gc - 1
                                 && result.RawVoronoiVertices.Count == gc - 1)
                            branch = "COLLINEAR PERPENDICULAR BISECTORS  [Cases 2 & 3]";
                        else
                            branch = "FULL VORONOI  [Cases 4 / general]";

                        Log($"  Branch taken  : {branch}");
                        Log($"  Raw Voronoi edges:    {result.RawVoronoiEdges.Count}");
                        Log($"  Raw Voronoi vertices: {result.RawVoronoiVertices.Count}");

                        for (int vi = 0; vi < result.RawVoronoiVertices.Count; vi++)
                        {
                            var v = result.RawVoronoiVertices[vi];
                            Log($"  RawVertex[{vi}]  ({v.X * 304.8:F1},{v.Y * 304.8:F1})mm");
                        }
                        for (int ei = 0; ei < result.RawVoronoiEdges.Count; ei++)
                        {
                            var e = result.RawVoronoiEdges[ei];
                            double lenMm = e.Start.DistanceTo(e.End) * 304.8;
                            string warn = lenMm < 1.0 ? "  ⚠ NEAR-ZERO LENGTH" : "";
                            Log($"  RawEdge[{ei}]" +
                                $"  S=({e.Start.X * 304.8:F1},{e.Start.Y * 304.8:F1})mm" +
                                $"  E=({e.End.X * 304.8:F1},{e.End.Y * 304.8:F1})mm" +
                                $"  Len={lenMm:F1}mm{warn}");
                        }
                    });

                    // Step 4: Clipping
                    Dispatch(() => { UpdateProgress("Clipping Voronoi to roof boundary…"); Log("[STEP 4] Clipping Voronoi to roof boundary…"); });
                    _clippingSvc.ClipAndCollect(result, boundary, result.DrainGroups);
                    Dispatch(() =>
                    {
                        Log($"  Clipped edges:  {result.ClippedEdges.Count}");
                        Log($"  Shape points:   {result.ShapePoints.Count}");
                        for (int ei = 0; ei < result.ClippedEdges.Count; ei++)
                        {
                            var e = result.ClippedEdges[ei];
                            double lenMm = e.Start.DistanceTo(e.End) * 304.8;
                            Log($"  ClippedEdge[{ei}]" +
                                $"  S=({e.Start.X * 304.8:F1},{e.Start.Y * 304.8:F1})mm" +
                                $"  E=({e.End.X * 304.8:F1},{e.End.Y * 304.8:F1})mm" +
                                $"  Len={lenMm:F1}mm");
                        }
                        for (int si = 0; si < result.ShapePoints.Count; si++)
                        {
                            var sp = result.ShapePoints[si];
                            result.ShapePointGroupMap.TryGetValue(si, out var gIdxs);
                            string gStr = gIdxs != null ? string.Join(",", gIdxs.Select(x => $"G{x}")) : "?";
                            Log($"  ShapePoint[{si}]  ({sp.X * 304.8:F1},{sp.Y * 304.8:F1})mm  Groups={gStr}");
                        }
                    });

                    // Step 5: Validation
                    Dispatch(() => { UpdateProgress("Validating equidistance…"); Log("[STEP 5] Validating equidistance…"); });
                    _validationSvc.ToleranceFeet = DrainGroupingService.MmToFeet(ValidationToleranceMm);
                    _validationSvc.Validate(result, result.DrainGroups);
                    Dispatch(() =>
                    {
                        Log($"  Validation tolerance: {ValidationToleranceMm} mm");
                        Log($"  Pass: {result.PassCount}   Fail: {result.FailCount}   Total: {result.ValidationLog.Count}");
                        foreach (var ve in result.ValidationLog)
                        {
                            string distStr = string.Join("  ", ve.DistancesToGroups.Select(
                                kv => $"G{kv.Key}={kv.Value * 304.8:F1}mm"));
                            Log($"  Pt[{ve.PointIndex}]  ({ve.X * 304.8:F1},{ve.Y * 304.8:F1})mm" +
                                $"  Dev={ve.MaxDeviation * 304.8:F2}mm" +
                                $"  {(ve.Passed ? "PASS" : "FAIL")}" +
                                $"  [{distStr}]" +
                                (string.IsNullOrEmpty(ve.Note) ? "" : $"  [{ve.Note}]"));
                        }
                    });

                    _lastResult = result;
                });

                // ── Step 6: Revit element creation (must be on UI thread) ─────────
                UpdateProgress("Creating Revit elements…");
                Log("[STEP 6] Creating Revit elements…");

                _creationSvc.CreateAll(_doc, _uiDoc.ActiveView, _selectedRoof, _lastResult);

                Log($"  Detail lines created: {_lastResult.CreatedDetailLineIds.Count}");
                Log($"  Shape points created: {_lastResult.CreatedShapePointIds.Count}");
                foreach (var id in _lastResult.CreatedDetailLineIds)
                    Log($"    DetailLine  Id={id}");

                // ── Populate UI ───────────────────────────────────────────────────
                foreach (var entry in _lastResult.ValidationLog)
                    ValidationLog.Add(entry);

                PassCount = _lastResult.PassCount;
                FailCount = _lastResult.FailCount;

                var elapsed = DateTime.Now - runStart;
                Log($"[END]  Elapsed: {elapsed.TotalSeconds:F2}s");
                Log($"[RESULT]  {_lastResult.ClippedEdges.Count} ridge lines" +
                    $"  {_lastResult.ShapePoints.Count} shape points" +
                    $"  Validation: {PassCount} PASS / {FailCount} FAIL");

                StatusMessage =
                    $"Done. {_lastResult.ClippedEdges.Count} ridge lines, " +
                    $"{_lastResult.ShapePoints.Count} shape points. " +
                    $"Validation: {PassCount} PASS / {FailCount} FAIL. " +
                    $"({elapsed.TotalSeconds:F1}s)";
            }
            catch (Exception ex)
            {
                Log($"[ERROR]  {ex.GetType().Name}: {ex.Message}");
                if (ex.InnerException != null)
                    Log($"  Inner: {ex.InnerException.Message}");
                Log($"  StackTrace:\r\n{ex.StackTrace}");

                StatusMessage = ex.GetType().Name == "ConvexHullGenerationException"
                    ? $"Voronoi failed: {ex.Message}. Groups: {GroupCountDisplay}. " +
                      "Need 3+ non-collinear groups."
                    : $"Error: {ex.Message}";
            }
            finally
            {
                IsBusy = false;
                ProgressDetail = string.Empty;
            }
        }

        private bool CanRun() => _selectedRoof != null && DrainCount >= 2 && ProximityDistanceMm > 0;

        // ── Log helpers ───────────────────────────────────────────────────────────

        /// <summary>
        /// Appends a line to PipelineLog on the UI thread and keeps
        /// PipelineLogText in sync so the read-only TextBox stays current.
        /// </summary>
        private void Log(string message)
        {
            void Append()
            {
                PipelineLog.Add(message);
                // Append incrementally rather than rebuilding from scratch each time
                PipelineLogText = PipelineLogText.Length == 0
                    ? message
                    : PipelineLogText + "\r\n" + message;
            }

            if (Application.Current?.Dispatcher?.CheckAccess() == true)
                Append();
            else
                Application.Current?.Dispatcher?.Invoke(Append);
        }

        /// <summary>Marshals an action to the UI thread (used inside Task.Run).</summary>
        private static void Dispatch(Action action)
            => Application.Current?.Dispatcher?.Invoke(action);

        private void UpdateProgress(string message) => ProgressDetail = message;

        // ── Helper filter ─────────────────────────────────────────────────────────

        private class RoofSelectionFilter : ISelectionFilter
        {
            public bool AllowElement(Element elem) => elem is RoofBase;
            public bool AllowReference(Reference reference, XYZ position) => false;
        }
    }
}