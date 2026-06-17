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
using Revit26_Plugin.RoofTools.LineAndPoints.RoofRidgeLines.V55.Models;
using Revit26_Plugin.RoofTools.LineAndPoints.RoofRidgeLines.V55.Services;

namespace Revit26_Plugin.RoofTools.LineAndPoints.RoofRidgeLines.V55.ViewModels
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
        private readonly InnerLoopService _innerLoopSvc;
        private readonly InnerLoopIntersectionService _innerLoopIntersectionSvc;

        // ── Internal state ────────────────────────────────────────────────────────
        private RoofBase _selectedRoof;
        private List<XYZ> _selectedDrainLocations = new List<XYZ>();
        private VoronoiRidgeResult _lastResult;
        private Window _ownerWindow;

        // ── Pipeline log ──────────────────────────────────────────────────────────
        public ObservableCollection<string> PipelineLog { get; }
            = new ObservableCollection<string>();

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
            _innerLoopSvc = new InnerLoopService();
            _innerLoopIntersectionSvc = new InnerLoopIntersectionService();
        }

        public void SetOwnerWindow(Window window) => _ownerWindow = window;

        // ── Standard observable properties ────────────────────────────────────────

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

        // ── 5 Status Cards ────────────────────────────────────────────────────────

        [ObservableProperty]
        private string _firstResetStatus = "—";

        [ObservableProperty]
        private int _totalCalculatedPoints;

        [ObservableProperty]
        private int _totalAddedPoints;

        [ObservableProperty]
        private int _totalFailedPoints;

        [ObservableProperty]
        private string _lastResetStatus = "—";

        // ── Validation log ────────────────────────────────────────────────────────

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

        [RelayCommand]
        private void CopyLog()
        {
            if (PipelineLog.Count == 0 && ValidationLog.Count == 0)
            {
                StatusMessage = "No log data to copy. Run the command first.";
                return;
            }
            var sb = new StringBuilder();
            sb.AppendLine("════════════════════════════════════════════════════════════");
            sb.AppendLine("  ROOF RIDGE LINES V55 – PIPELINE LOG");
            sb.AppendLine($"  Copied: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            sb.AppendLine("════════════════════════════════════════════════════════════");
            foreach (var line in PipelineLog) sb.AppendLine(line);

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
                sb.AppendLine($"SUMMARY\tPass: {PassCount}\tFail: {FailCount}\tTotal: {ValidationLog.Count}");
            }
            try
            {
                Clipboard.SetText(sb.ToString());
                StatusMessage = $"Full log copied to clipboard ({PipelineLog.Count} lines, {ValidationLog.Count} validation rows).";
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

            // Reset cards
            FirstResetStatus = "Pending…";
            TotalCalculatedPoints = 0;
            TotalAddedPoints = 0;
            TotalFailedPoints = 0;
            LastResetStatus = "Pending…";

            var runStart = DateTime.Now;
            Log($"[START]  {runStart:HH:mm:ss}");
            Log($"[INPUT]  Roof: {_selectedRoof?.Name}  Id={_selectedRoof?.Id}");
            Log($"[INPUT]  Raw drain points: {_selectedDrainLocations.Count}");
            Log($"[INPUT]  Proximity distance: {ProximityDistanceMm} mm");
            Log($"[INPUT]  Validation tolerance: {ValidationToleranceMm} mm");

            for (int i = 0; i < _selectedDrainLocations.Count; i++)
            {
                var p = _selectedDrainLocations[i];
                Log($"  Drain[{i}]  X={p.X:F4} ft ({p.X * 304.8:F1} mm)  Y={p.Y:F4} ft ({p.Y * 304.8:F1} mm)  Z={p.Z:F4} ft");
            }

            try
            {
                // ── Step 1: Drain grouping ─────────────────────────────────────────
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
                        $"  Centroid X={g.Centroid.X:F4} ft  Y={g.Centroid.Y:F4} ft");
                }

                if (groupCount < 2)
                    throw new InvalidOperationException(
                        "At least two distinct drain groups are required.");

                if (groupCount < 3)
                    Log($"  [WARN] Only {groupCount} groups — midline bisector mode will be used.");

                // ── Step 2: Boundary + inner-loop extraction ────────────────────────
                // These call into the Revit API (get_Geometry, GetDependentElements,
                // GetElement) and therefore MUST run on the UI/API thread. They run
                // here, before Task.Run, rather than inside it.
                var result = new VoronoiRidgeResult();
                result.DrainGroups = drainGroups;

                UpdateProgress("Extracting roof boundary…");
                Log("[STEP 2] Extracting roof boundary…");
                List<XYZ> boundary = _boundarySvc.ExtractBoundary(_selectedRoof);

                // Store boundary + centroid for AddPoint fallback in RidgeCreationService
                result.BoundaryPolygon = boundary;
                result.RoofCentroid = new XYZ(
                    boundary.Average(p => p.X),
                    boundary.Average(p => p.Y),
                    0.0);

                Log($"  Boundary vertices: {boundary.Count}");
                {
                    double minX = boundary.Min(p => p.X), maxX = boundary.Max(p => p.X);
                    double minY = boundary.Min(p => p.Y), maxY = boundary.Max(p => p.Y);
                    Log($"  Boundary BBox  X=[{minX * 304.8:F1}, {maxX * 304.8:F1}] mm  Y=[{minY * 304.8:F1}, {maxY * 304.8:F1}] mm");
                }

                UpdateProgress("Detecting inner loops…");
                Log("[STEP 2a] Detecting inner loops…");
                result.InnerLoops = _innerLoopSvc.ExtractInnerLoops(_selectedRoof);
                Log($"  Inner loops found: {result.InnerLoops.Count}");

                // ── Steps 3–5: pure geometry/math — no Revit API calls, safe to run
                //              on a background thread for UI responsiveness ─────────
                await Task.Run(() =>
                {
                    // Step 3: Voronoi
                    Dispatch(() => { UpdateProgress("Computing Voronoi diagram…"); Log("[STEP 3] Computing Voronoi diagram…"); });
                    _voronoiSvc.Compute(result.DrainGroups, result);
                    Dispatch(() =>
                    {
                        Log($"  Raw Voronoi edges:    {result.RawVoronoiEdges.Count}");
                        Log($"  Raw Voronoi vertices: {result.RawVoronoiVertices.Count}");
                    });

                    // Step 4: Clipping
                    Dispatch(() => { UpdateProgress("Clipping Voronoi to roof boundary…"); Log("[STEP 4] Clipping Voronoi to roof boundary…"); });
                    _clippingSvc.ClipAndCollect(result, boundary, result.DrainGroups);
                    Dispatch(() =>
                    {
                        Log($"  Clipped edges:  {result.ClippedEdges.Count}");
                        Log($"  Shape points:   {result.ShapePoints.Count}");
                    });

                    // Step 4a: Ridge edge mid-points (one per clipped Voronoi edge)
                    result.RidgeEdgeMidPoints = result.ClippedEdges
                        .Select(e => new XYZ((e.Start.X + e.End.X) / 2.0,
                                             (e.Start.Y + e.End.Y) / 2.0, 0.0))
                        .ToList();
                    Dispatch(() => Log($"  Ridge mid-points: {result.RidgeEdgeMidPoints.Count}"));

                    // Step 4b: Inner-loop × ridge-line intersections (pure math — the
                    //          loops themselves were already extracted on the UI thread above)
                    if (result.InnerLoops.Count > 0)
                    {
                        Dispatch(() => Log("[STEP 4b] Computing inner-loop intersections…"));
                        int innerPtCount = _innerLoopIntersectionSvc.ComputeInnerLoopIntersections(result, boundary);
                        Dispatch(() => Log($"  Inner loop intersection points: {innerPtCount}"));
                    }

                    // Step 5: Validation
                    Dispatch(() => { UpdateProgress("Validating equidistance…"); Log("[STEP 5] Validating equidistance…"); });
                    _validationSvc.ToleranceFeet = DrainGroupingService.MmToFeet(ValidationToleranceMm);
                    _validationSvc.Validate(result, result.DrainGroups);
                    Dispatch(() =>
                    {
                        Log($"  Pass: {result.PassCount}   Fail: {result.FailCount}   Total: {result.ValidationLog.Count}");
                    });

                    _lastResult = result;
                });

                // ── Step 6: Revit element creation (UI thread) ─────────────────────
                UpdateProgress("Creating Revit elements…");
                Log("[STEP 6] Creating Revit elements…");

                // Wire up per-TX card callbacks before calling CreateAll
                _creationSvc.OnFirstResetDone = () =>
                {
                    FirstResetStatus = _creationSvc.FirstResetStatus;
                    Log($"  [TX-1] First Reset: {FirstResetStatus}");
                };

                _creationSvc.OnPointsDone = () =>
                {
                    TotalCalculatedPoints = _creationSvc.TotalCalculated;
                    TotalAddedPoints = _creationSvc.TotalAdded;
                    TotalFailedPoints = _creationSvc.TotalFailed;
                    Log($"  [TX-2] Points — Calculated: {TotalCalculatedPoints}  Added: {TotalAddedPoints}  Failed: {TotalFailedPoints}");
                };

                _creationSvc.OnLastResetDone = () =>
                {
                    LastResetStatus = _creationSvc.LastResetStatus;
                    Log($"  [TX-3] Last Reset: {LastResetStatus}");
                };

                _creationSvc.CreateAll(_doc, _uiDoc.ActiveView, _selectedRoof, _lastResult);

                Log($"  Detail lines created: {_lastResult.CreatedDetailLineIds.Count}");

                // ── Populate validation log ────────────────────────────────────────
                foreach (var entry in _lastResult.ValidationLog)
                    ValidationLog.Add(entry);

                PassCount = _lastResult.PassCount;
                FailCount = _lastResult.FailCount;

                var elapsed = DateTime.Now - runStart;
                Log($"[END]  Elapsed: {elapsed.TotalSeconds:F2}s");

                StatusMessage =
                    $"Done. {_lastResult.ClippedEdges.Count} ridge lines, " +
                    $"{TotalAddedPoints}/{TotalCalculatedPoints} shape points added. " +
                    $"Validation: {PassCount} PASS / {FailCount} FAIL. ({elapsed.TotalSeconds:F1}s)";
            }
            catch (Exception ex)
            {
                Log($"[ERROR]  {ex.GetType().Name}: {ex.Message}");
                if (ex.InnerException != null)
                    Log($"  Inner: {ex.InnerException.Message}");
                StatusMessage = $"Error: {ex.Message}";
            }
            finally
            {
                IsBusy = false;
                ProgressDetail = string.Empty;
            }
        }

        private bool CanRun() => _selectedRoof != null && DrainCount >= 2 && ProximityDistanceMm > 0;

        // ── Log helpers ───────────────────────────────────────────────────────────

        private void Log(string message)
        {
            void Append()
            {
                PipelineLog.Add(message);
                PipelineLogText = PipelineLogText.Length == 0
                    ? message
                    : PipelineLogText + "\r\n" + message;
            }
            if (Application.Current?.Dispatcher?.CheckAccess() == true)
                Append();
            else
                Application.Current?.Dispatcher?.Invoke(Append);
        }

        private static void Dispatch(Action action)
            => Application.Current?.Dispatcher?.Invoke(action);

        private void UpdateProgress(string message) => ProgressDetail = message;

        // ── Roof selection filter ─────────────────────────────────────────────────

        private class RoofSelectionFilter : ISelectionFilter
        {
            public bool AllowElement(Element elem) => elem is RoofBase;
            public bool AllowReference(Reference reference, XYZ position) => false;
        }
    }
}