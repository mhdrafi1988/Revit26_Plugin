using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;
using Revit26_Plugin.RoofTools.LineAndPoints.RoofRidgeLines.V60.Models;
using Revit26_Plugin.RoofTools.LineAndPoints.RoofRidgeLines.V60.Services;

namespace Revit26_Plugin.RoofTools.LineAndPoints.RoofRidgeLines.V60.ViewModels
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
        private VoronoiRidgeResult _lastResult;
        private Window _ownerWindow;

        // ── Pipeline log ──────────────────────────────────────────────────────────
        public ObservableCollection<string> PipelineLog { get; }
            = new ObservableCollection<string>();

        [ObservableProperty]
        private string _pipelineLogText = string.Empty;

        // ── Constructors ──────────────────────────────────────────────────────────

        /// <summary>
        /// Primary constructor. If a roof is provided, it loads the openings immediately.
        /// </summary>
        public RoofRidgeViewModel(UIDocument uiDoc, RoofBase roof = null)
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

            // Hook collection changes to listen to individual data grid item properties
            Openings.CollectionChanged += OnOpeningsCollectionChanged;

            // If a roof was pre‑selected in the command, load its openings immediately.
            if (roof != null)
            {
                _selectedRoof = roof;
                RoofDescription = $"Roof: {_selectedRoof.Name}  (Id {_selectedRoof.Id})";
                LoadOpeningsForCurrentRoof();
                StatusMessage = $"Loaded {Openings.Count} inner loops. Select at least 2 as drainage seeds.";
            }
            else
            {
                RoofDescription = "No roof selected";
                StatusMessage = "Select a roof using the 'Select Roof' button.";
            }
        }

        public void SetOwnerWindow(Window window) => _ownerWindow = window;

        // ── Observable properties ─────────────────────────────────────────────────

        [ObservableProperty]
        [NotifyCanExecuteChangedFor(nameof(RunCommand))]
        private string _roofDescription = "No roof selected";

        [ObservableProperty]
        [NotifyCanExecuteChangedFor(nameof(RunCommand))]
        private int _selectedOpeningsCount;

        [ObservableProperty]
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

        // ── Status Cards ──────────────────────────────────────────────────────────

        [ObservableProperty]
        private int _totalCalculatedPoints; // Used as selection summary card on top-left

        [ObservableProperty]
        private int _totalAddedPoints;

        [ObservableProperty]
        private int _totalFailedPoints;

        // ── Validation log ────────────────────────────────────────────────────────

        public ObservableCollection<ValidationEntry> ValidationLog { get; }
            = new ObservableCollection<ValidationEntry>();

        // ── Openings collection ───────────────────────────────────────────────────

        public ObservableCollection<OpeningData> Openings { get; }
            = new ObservableCollection<OpeningData>();

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

                LoadOpeningsForCurrentRoof();
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
        private void SelectAllOpenings()
        {
            foreach (var opening in Openings)
            {
                opening.IsSelected = true;
            }
            UpdateSelectedCount();
        }

        [RelayCommand]
        private void SelectNoOpenings()
        {
            foreach (var opening in Openings)
            {
                opening.IsSelected = false;
            }
            UpdateSelectedCount();
        }

        [RelayCommand]
        private void InverseOpeningSelection()
        {
            foreach (var opening in Openings)
            {
                opening.IsSelected = !opening.IsSelected;
            }
            UpdateSelectedCount();
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
            sb.AppendLine("  ROOF RIDGE LINES V60 – PIPELINE LOG");
            sb.AppendLine($"  Copied: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            sb.AppendLine("════════════════════════════════════════════════════════════");
            foreach (var line in PipelineLog) sb.AppendLine(line);

            if (ValidationLog.Count > 0)
            {
                sb.AppendLine();
                sb.AppendLine("════════════════════════════════════════════════════════════");
                sb.AppendLine("  VALIDATION TABLE  (tab‑separated — paste into Excel)");
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

        // ── Run Execution Loop ────────────────────────────────────────────────────

        [RelayCommand(CanExecute = nameof(CanRun))]
        private async Task Run()
        {
            IsBusy = true;
            ValidationLog.Clear();
            PipelineLog.Clear();
            PipelineLogText = string.Empty;
            StatusMessage = "Running Voronoi ridge generation…";

            TotalAddedPoints = 0;
            TotalFailedPoints = 0;

            var runStart = DateTime.Now;
            Log($"[START]  {runStart:HH:mm:ss}");
            Log($"[INPUT]  Roof: {_selectedRoof?.Name}  Id={_selectedRoof?.Id}");
            Log($"[INPUT]  Proximity distance: {ProximityDistanceMm} mm");
            Log($"[INPUT]  Validation tolerance: {ValidationToleranceMm} mm");

            try
            {
                UpdateProgress("Extracting roof boundary…");
                Log("[STEP 1] Extracting roof boundary…");
                List<XYZ> boundary = _boundarySvc.ExtractBoundary(_selectedRoof);

                var result = new VoronoiRidgeResult();
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
                Log("[STEP 2] Detecting inner loops…");
                result.InnerLoops = _innerLoopSvc.ExtractInnerLoops(_selectedRoof);
                Log($"  Inner loops found: {result.InnerLoops.Count}");

                var selected = Openings.Where(o => o.IsSelected).ToList();
                if (selected.Count < 2)
                {
                    StatusMessage = "Please select at least 2 inner loops as drainage seeds.";
                    IsBusy = false;
                    return;
                }
                SelectedOpeningsCount = selected.Count;
                Log($"  Selected {selected.Count} loops as seeds.");

                var drainPoints = selected.Select(o => o.Centroid).ToList();
                var loopsToIntersect = result.InnerLoops
                    .Where(loop => !selected.Any(sel => AreLoopsEqual(loop, sel.Vertices)))
                    .ToList();
                Log($"  {loopsToIntersect.Count} unselected loops will terminate ridges.");

                await Task.Run(() =>
                {
                    // Step 3: Drain grouping
                    Dispatch(() => { UpdateProgress("Grouping drain seeds…"); Log("[STEP 3] Grouping drain seeds…"); });
                    double proxFeet = DrainGroupingService.MmToFeet(ProximityDistanceMm);
                    var drainGroups = _groupingSvc.GroupDrains(drainPoints, proxFeet);
                    result.DrainGroups = drainGroups;
                    Dispatch(() =>
                    {
                        GroupCountDisplay = drainGroups.Count.ToString();
                        Log($"  Groups formed: {drainGroups.Count}");
                        foreach (var g in drainGroups)
                            Log($"  Group[{g.GroupIndex}]  Drains={g.DrainLocations.Count}  Centroid X={g.Centroid.X:F4} ft  Y={g.Centroid.Y:F4} ft");
                        if (drainGroups.Count < 2)
                            throw new InvalidOperationException("At least two distinct drain groups are required.");
                    });

                    // Step 4: Voronoi diagram computing
                    Dispatch(() => { UpdateProgress("Computing Voronoi diagram…"); Log("[STEP 4] Computing Voronoi diagram…"); });
                    _voronoiSvc.Compute(result.DrainGroups, result);
                    Dispatch(() =>
                    {
                        Log($"  Raw Voronoi edges:    {result.RawVoronoiEdges.Count}");
                        Log($"  Raw Voronoi vertices: {result.RawVoronoiVertices.Count}");
                    });

                    // Step 5: Clipping to boundary
                    Dispatch(() => { UpdateProgress("Clipping Voronoi to roof boundary…"); Log("[STEP 5] Clipping Voronoi to roof boundary…"); });
                    _clippingSvc.ClipAndCollect(result, boundary, result.DrainGroups);
                    Dispatch(() =>
                    {
                        Log($"  Clipped edges:  {result.ClippedEdges.Count}");
                        Log($"  Shape points:   {result.ShapePoints.Count}");
                    });

                    result.RidgeEdgeMidPoints = result.ClippedEdges
                        .Select(e => new XYZ((e.Start.X + e.End.X) / 2.0,
                                             (e.Start.Y + e.End.Y) / 2.0, 0.0))
                        .ToList();
                    Dispatch(() => Log($"  Ridge mid-points: {result.RidgeEdgeMidPoints.Count}"));

                    if (loopsToIntersect.Count > 0)
                    {
                        Dispatch(() => Log("[STEP 5b] Computing inner-loop intersections (unselected only)…"));
                        int innerPtCount = _innerLoopIntersectionSvc.ComputeInnerLoopIntersections(
                            result, boundary, loopsToIntersect);
                        Dispatch(() => Log($"  Inner loop intersection points: {innerPtCount}"));
                    }
                    else
                    {
                        Dispatch(() => Log("[STEP 5b] All loops selected — no termination points added."));
                    }

                    // Step 6: Grid validation
                    Dispatch(() => { UpdateProgress("Validating equidistance…"); Log("[STEP 6] Validating equidistance…"); });
                    _validationSvc.ToleranceFeet = DrainGroupingService.MmToFeet(ValidationToleranceMm);
                    _validationSvc.Validate(result, result.DrainGroups);
                    Dispatch(() =>
                    {
                        Log($"  Pass: {result.PassCount}   Fail: {result.FailCount}   Total: {result.ValidationLog.Count}");
                    });

                    _lastResult = result;
                });

                // Step 7: Element Creation inside Revit Document
                UpdateProgress("Creating Revit elements…");
                Log("[STEP 7] Creating Revit elements…");

                _creationSvc.OnPointsDone = () =>
                {
                    TotalAddedPoints = _creationSvc.TotalAdded;
                    TotalFailedPoints = _creationSvc.TotalFailed;
                    Log($"  [TX-04] Points — Calculated: {_creationSvc.TotalCalculated}  Added: {TotalAddedPoints}  Failed: {TotalFailedPoints}");
                };

                _creationSvc.CreateAll(_doc, _uiDoc.ActiveView, _selectedRoof, _lastResult);
                Log($"  Detail lines created: {_lastResult.CreatedDetailLineIds.Count}");

                foreach (var entry in _lastResult.ValidationLog)
                    ValidationLog.Add(entry);

                PassCount = _lastResult.PassCount;
                FailCount = _lastResult.FailCount;

                var elapsed = DateTime.Now - runStart;
                Log($"[END]  Elapsed: {elapsed.TotalSeconds:F2}s");

                StatusMessage =
                    $"Done. {_lastResult.ClippedEdges.Count} ridge lines, " +
                    $"{TotalAddedPoints}/{_creationSvc.TotalCalculated} shape points added. " +
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

        private bool CanRun() => _selectedRoof != null && SelectedOpeningsCount >= 2 && ProximityDistanceMm > 0;

        // ── Data Loading & Property Event Aggregators ─────────────────────────────

        private void LoadOpeningsForCurrentRoof()
        {
            if (_selectedRoof == null) return;

            try
            {
                StatusMessage = "Extracting inner loops from roof...";
                var loops = _innerLoopSvc.ExtractInnerLoops(_selectedRoof);

                Openings.Clear();
                for (int i = 0; i < loops.Count; i++)
                {
                    var loop = loops[i];
                    var data = OpeningAnalyzerService.AnalyzeLoop(loop);
                    data.Index = i;
                    Openings.Add(data);
                }

                string logMsg = $"[OPENINGS] Loaded {Openings.Count} inner loop(s).";
                PipelineLog.Add(logMsg);
                PipelineLogText = PipelineLogText.Length == 0 ? logMsg : PipelineLogText + "\r\n" + logMsg;

                UpdateSelectedCount();
            }
            catch (Exception ex)
            {
                StatusMessage = $"Failed to load openings: {ex.Message}";
                PipelineLog.Add($"[ERROR] {ex.Message}");
            }
        }

        private void OnOpeningsCollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            if (e.NewItems != null)
            {
                foreach (INotifyPropertyChanged item in e.NewItems.OfType<INotifyPropertyChanged>())
                    item.PropertyChanged += OnOpeningItemPropertyChanged;
            }
            if (e.OldItems != null)
            {
                foreach (INotifyPropertyChanged item in e.OldItems.OfType<INotifyPropertyChanged>())
                    item.PropertyChanged -= OnOpeningItemPropertyChanged;
            }
            UpdateSelectedCount();
        }

        private void OnOpeningItemPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(OpeningData.IsSelected))
            {
                UpdateSelectedCount();
            }
        }

        private void UpdateSelectedCount()
        {
            SelectedOpeningsCount = Openings.Count(o => o.IsSelected);
            TotalCalculatedPoints = SelectedOpeningsCount;

            // Explicitly force the framework command state evaluation
            RunCommand.NotifyCanExecuteChanged();
        }

        // ── Thread Helpers ────────────────────────────────────────────────────────

        private static bool AreLoopsEqual(List<XYZ> a, List<XYZ> b)
        {
            if (a.Count != b.Count) return false;
            for (int i = 0; i < a.Count; i++)
                if (a[i].DistanceTo(b[i]) > 1e-6) return false;
            return true;
        }

        private void Log(string message)
        {
            void Append()
            {
                PipelineLog.Add(message);
                PipelineLogText = PipelineLogText.Length == 0 ? message : PipelineLogText + "\r\n" + message;
            }
            if (Application.Current?.Dispatcher?.CheckAccess() == true) Append();
            else Application.Current?.Dispatcher?.Invoke(Append);
        }

        private static void Dispatch(Action action) => Application.Current?.Dispatcher?.Invoke(action);

        private void UpdateProgress(string message) => ProgressDetail = message;

        private class RoofSelectionFilter : ISelectionFilter
        {
            public bool AllowElement(Element elem) => elem is RoofBase;
            public bool AllowReference(Reference reference, XYZ position) => false;
        }
    }
}