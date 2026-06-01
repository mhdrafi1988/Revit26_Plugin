// =======================================================
// File: ViewModels/AutoSlopeMergedViewModel.cs
// Description: Main ViewModel for AutoSlopeWindow
// =======================================================

using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Revit26_Plugin.AutoSlopeByDrain_21.Commands;
using Revit26_Plugin.AutoSlopeByDrain_21.Models;
using Revit26_Plugin.AutoSlopeByDrain_21.Services.Interfaces;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Data;
using System.Windows.Input;

namespace Revit26_Plugin.AutoSlopeByDrain_21.ViewModels
{
    public class AutoSlopeMergedViewModel : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string name = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }

        // ── Services ──────────────────────────────────────────────
        private readonly IDrainDetectionService _drainDetectionService;
        private readonly IRoofSlopeProcessorService _slopeProcessorService;
        private readonly IExcelExportService _excelExportService;
        private readonly IDialogService _dialogService;
        private readonly ILoggerService _loggerService;
        private readonly IUnitConversionService _unitService;

        // ── Revit context ────────────────────────────────────────
        private readonly UIDocument _uidoc;
        private readonly UIApplication _app;
        private readonly ElementId _roofId;
        private readonly List<XYZ> _initialDrains;
        private RoofData _currentRoof;
        private RoofBase _currentRoofElement;       // Store actual roof for export
        private SlopeResult _lastSlopeResult;       // Store last run results

        // ── Close hook ───────────────────────────────────────────
        public Action CloseWindow { get; set; }

        // ══════════════════════════════════════════════════════════
        //  COMMANDS
        // ══════════════════════════════════════════════════════════
        public ICommand RunCommand { get; }
        public ICommand ExportResultsCommand { get; }
        public ICommand BrowseFolderCommand { get; }
        public ICommand ClearLogCommand { get; }
        public ICommand CopyLogCommand { get; }
        public ICommand SelectAllCommand { get; }
        public ICommand SelectNoneCommand { get; }
        public ICommand InvertSelectionCommand { get; }
        public ICommand CancelCommand { get; }
        public ICommand OkCommand { get; }

        // ══════════════════════════════════════════════════════════
        //  LEFT-PANEL — SLOPE
        // ══════════════════════════════════════════════════════════

        private string _slopeInput = "1.5";
        public string SlopeInput
        {
            get => _slopeInput;
            set
            {
                _slopeInput = value;
                OnPropertyChanged();
                if (double.TryParse(value, out double d) && d > 0)
                {
                    _loggerService.LogInfo($"Slope set to: {d}%");
                    SlopePercent = d;
                }
            }
        }

        public List<string> SlopeOptions { get; } = new List<string> { "1.0", "1.5", "2.0", "2.5", "3.0" };

        private double _thresholdMeters = 5.0;
        public double ThresholdMeters
        {
            get => _thresholdMeters;
            set { _thresholdMeters = value; OnPropertyChanged(); }
        }

        // ══════════════════════════════════════════════════════════
        //  LEFT-PANEL — DRAIN DETECTION
        // ══════════════════════════════════════════════════════════

        private bool _enableDrainTolerance = true;
        public bool EnableDrainTolerance
        {
            get => _enableDrainTolerance;
            set { _enableDrainTolerance = value; OnPropertyChanged(); }
        }

        private double _drainToleranceMm = 5.0;
        public double DrainToleranceMm
        {
            get => _drainToleranceMm;
            set { _drainToleranceMm = value; OnPropertyChanged(); }
        }

        // ══════════════════════════════════════════════════════════
        //  LEFT-PANEL — EXPORT
        // ══════════════════════════════════════════════════════════

        private bool _exportToExcel = true;
        public bool ExportToExcel
        {
            get => _exportToExcel;
            set
            {
                _exportToExcel = value;
                OnPropertyChanged();
                ((RelayCommand)ExportResultsCommand).RaiseCanExecuteChanged();
            }
        }

        private bool _includeVertexDetails = true;
        public bool IncludeVertexDetails
        {
            get => _includeVertexDetails;
            set { _includeVertexDetails = value; OnPropertyChanged(); }
        }

        private string _exportFolderPath;
        public string ExportFolderPath
        {
            get => _exportFolderPath;
            set
            {
                _exportFolderPath = value;
                OnPropertyChanged();
                _loggerService.LogInfo($"Export folder set to: {value}");
            }
        }

        // ══════════════════════════════════════════════════════════
        //  LEFT-PANEL — STATUS
        // ══════════════════════════════════════════════════════════

        private bool _hasRun;
        public bool HasRun
        {
            get => _hasRun;
            set { _hasRun = value; OnPropertyChanged(); }
        }

        private bool _isComplete;
        public bool IsComplete
        {
            get => _isComplete;
            set { _isComplete = value; OnPropertyChanged(); }
        }

        private string _statusMessage = "Ready";
        public string StatusMessage
        {
            get => _statusMessage;
            set { _statusMessage = value; OnPropertyChanged(); }
        }

        private bool _slopeApplied;
        public bool SlopeApplied
        {
            get => _slopeApplied;
            set
            {
                _slopeApplied = value;
                OnPropertyChanged();
                ((RelayCommand)RunCommand).RaiseCanExecuteChanged();
                ((RelayCommand)ExportResultsCommand).RaiseCanExecuteChanged();
            }
        }

        // ══════════════════════════════════════════════════════════
        //  RIGHT-PANEL — METRIC CARDS
        // ══════════════════════════════════════════════════════════

        private double _longestPath_m;
        public double LongestPath_m
        {
            get => _longestPath_m;
            set { _longestPath_m = value; OnPropertyChanged(); }
        }

        private double _highestElevation_mm;
        public double HighestElevation_mm
        {
            get => _highestElevation_mm;
            set { _highestElevation_mm = value; OnPropertyChanged(); }
        }

        private double _avgSlopePercent;
        public double AvgSlopePercent
        {
            get => _avgSlopePercent;
            set { _avgSlopePercent = value; OnPropertyChanged(); }
        }

        private double _runDuration_sec;
        public double RunDuration_sec
        {
            get => _runDuration_sec;
            set { _runDuration_sec = value; OnPropertyChanged(); }
        }

        private int _verticesSkipped;
        public int VerticesSkipped
        {
            get => _verticesSkipped;
            set { _verticesSkipped = value; OnPropertyChanged(); }
        }

        private int _pickedDrainCount;
        public int PickedDrainCount
        {
            get => _pickedDrainCount;
            set { _pickedDrainCount = value; OnPropertyChanged(); }
        }

        private int _finalDrainCount;
        public int FinalDrainCount
        {
            get => _finalDrainCount;
            set { _finalDrainCount = value; OnPropertyChanged(); }
        }

        private double _slopePercent;
        public double SlopePercent
        {
            get => _slopePercent;
            set { _slopePercent = value; OnPropertyChanged(); }
        }

        private double _percentage2Applied;
        public double Percentage2Applied
        {
            get => _percentage2Applied;
            set { _percentage2Applied = value; OnPropertyChanged(); }
        }

        private int _verticesProcessed;
        public int VerticesProcessed
        {
            get => _verticesProcessed;
            set { _verticesProcessed = value; OnPropertyChanged(); }
        }

        // ══════════════════════════════════════════════════════════
        //  RIGHT-PANEL — DRAIN DATA-GRID
        // ══════════════════════════════════════════════════════════

        public ObservableCollection<DrainItem> AllDrains { get; } = new ObservableCollection<DrainItem>();
        public ObservableCollection<string> SizeFilters { get; } = new ObservableCollection<string>();

        public ICollectionView FilteredDrainsView { get; }

        private string _selectedSizeFilter = "All";
        public string SelectedSizeFilter
        {
            get => _selectedSizeFilter;
            set
            {
                _selectedSizeFilter = value;
                OnPropertyChanged();
                FilteredDrainsView.Refresh();
                UpdateSelectedCount();
                _loggerService.LogInfo($"Filter applied: {value}");
            }
        }

        private int _selectedDrainsCount;
        public int SelectedDrainsCount
        {
            get => _selectedDrainsCount;
            set { _selectedDrainsCount = value; OnPropertyChanged(); }
        }

        private string _roofInfo;
        public string RoofInfo
        {
            get => _roofInfo;
            set { _roofInfo = value; OnPropertyChanged(); }
        }

        private string _resultsInfo;
        public string ResultsInfo
        {
            get => _resultsInfo;
            set { _resultsInfo = value; OnPropertyChanged(); }
        }

        // ══════════════════════════════════════════════════════════
        //  RIGHT-PANEL — LOG (bound to UI)
        // ══════════════════════════════════════════════════════════

        private string _logText = string.Empty;
        public string LogText
        {
            get => _logText;
            set { _logText = value; OnPropertyChanged(); }
        }

        // ══════════════════════════════════════════════════════════
        //  CONSTRUCTOR (with dependency injection)
        // ══════════════════════════════════════════════════════════

        public AutoSlopeMergedViewModel(
            UIDocument uidoc,
            UIApplication app,
            ElementId roofId,
            List<XYZ> drains,
            IDrainDetectionService drainDetectionService = null,
            IRoofSlopeProcessorService slopeProcessorService = null,
            IExcelExportService excelExportService = null,
            IDialogService dialogService = null,
            ILoggerService loggerService = null,
            IUnitConversionService unitService = null)
        {
            _uidoc = uidoc;
            _app = app;
            _roofId = roofId;
            _initialDrains = drains ?? new List<XYZ>();

            // Initialize services
            _unitService = unitService ?? new Services.Implementations.UnitConversionService();
            _loggerService = loggerService ?? new Services.Implementations.LoggerService();
            _drainDetectionService = drainDetectionService ?? new Services.Implementations.DrainDetectionService(_unitService);
            _dialogService = dialogService ?? new Services.Implementations.DialogService();

            // Create slope processor (real implementation)
            var realProcessor = new Services.Implementations.RoofSlopeProcessorService();
            _slopeProcessorService = slopeProcessorService ?? realProcessor;

            // Create Excel export service, passing the processor so it can retrieve vertex data
            _excelExportService = excelExportService ?? new Services.Implementations.ExcelExportService();

            // Wire up logger to update UI
            _loggerService.LogMessageAdded += (msg) =>
            {
                Application.Current?.Dispatcher.Invoke(() =>
                {
                    LogText += msg + "\n";
                });
            };

            // Wire commands
            RunCommand = new RelayCommand(ExecuteRun, CanRun);
            ExportResultsCommand = new RelayCommand(ExecuteExport, CanExport);
            BrowseFolderCommand = new RelayCommand(ExecuteBrowseFolder);
            ClearLogCommand = new RelayCommand(() => _loggerService.Clear());
            CopyLogCommand = new RelayCommand(ExecuteCopyLog);
            SelectAllCommand = new RelayCommand(SelectAll);
            SelectNoneCommand = new RelayCommand(SelectNone);
            InvertSelectionCommand = new RelayCommand(InvertSelection);
            CancelCommand = new RelayCommand(() => { _loggerService.Log("Cancelled."); CloseWindow?.Invoke(); });
            OkCommand = new RelayCommand(() => { _loggerService.Log("✓ Done."); CloseWindow?.Invoke(); });

            // Filtered collection view for DataGrid
            FilteredDrainsView = CollectionViewSource.GetDefaultView(AllDrains);
            FilteredDrainsView.Filter = FilterDrainItem;

            // Default export path
            ExportFolderPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                "AutoSlope_Reports");

            if (!Directory.Exists(ExportFolderPath))
                Directory.CreateDirectory(ExportFolderPath);

            // Seed size filter list
            SizeFilters.Add("All");

            // Initialise
            _loggerService.LogInfo("=== Auto Slope Engine Initialised ===");
            _loggerService.LogInfo($"Default export folder: {ExportFolderPath}");

            InitialiseRoof();
        }

        // ══════════════════════════════════════════════════════════
        //  INITIALISATION
        // ══════════════════════════════════════════════════════════

        private void InitialiseRoof()
        {
            try
            {
                var roof = _uidoc.Document.GetElement(_roofId) as RoofBase;
                if (roof != null)
                {
                    RoofInfo = $"{roof.Name}  (Id: {roof.Id})";
                    _loggerService.LogInfo($"Roof: {roof.Name} (Id: {roof.Id})");
                    _currentRoofElement = roof;  // Store for later use

                    _currentRoof = new RoofData
                    {
                        Id = roof.Id,
                        Name = roof.Name
                    };

                    // Get the top face and vertices from the roof
                    var topFace = GetRoofTopFace(roof);
                    var vertices = GetRoofVertices(roof);

                    if (topFace != null)
                    {
                        _loggerService.LogInfo("Detecting inner loop openings on top surface…");
                        _loggerService.LogInfo($"Using {DrainToleranceMm} mm tolerance for drain vertex identification");

                        // Detect drains using service
                        var detected = _drainDetectionService.DetectDrainsFromRoof(
                            roof, topFace, vertices, DrainToleranceMm, EnableDrainTolerance);

                        if (detected != null && detected.Any())
                        {
                            PopulateDrainGrid(detected);
                        }
                        else
                        {
                            _loggerService.LogWarning("No drains detected. Creating sample data for testing.");
                            CreateSampleDrainData();
                        }
                    }
                    else
                    {
                        _loggerService.LogWarning("Could not get top face from roof. Creating sample data.");
                        CreateSampleDrainData();
                    }
                }
                else
                {
                    RoofInfo = $"Id: {_roofId?.Value}";
                    _loggerService.LogInfo($"Roof Id: {_roofId?.Value}");
                    CreateSampleDrainData();
                }

                UpdateSelectedCount();
            }
            catch (Exception ex)
            {
                _loggerService.LogError($"Roof initialisation error: {ex.Message}");
                CreateSampleDrainData();
            }
        }

        private Face GetRoofTopFace(RoofBase roof)
        {
            try
            {
                var opt = new Options { ComputeReferences = true, DetailLevel = ViewDetailLevel.Medium };
                var geomElem = roof.get_Geometry(opt);

                foreach (GeometryObject geomObj in geomElem)
                {
                    if (geomObj is Solid solid && solid.Faces.Size > 0)
                    {
                        foreach (Face face in solid.Faces)
                        {
                            try
                            {
                                var bbox = face.GetBoundingBox();
                                var uvCenter = new UV(
                                    (bbox.Min.U + bbox.Max.U) / 2,
                                    (bbox.Min.V + bbox.Max.V) / 2);
                                var normal = face.ComputeNormal(uvCenter);
                                if (normal.Z > 0.5)
                                {
                                    return face;
                                }
                            }
                            catch { }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _loggerService.LogError($"Error getting roof top face: {ex.Message}");
            }
            return null;
        }

        private List<XYZ> GetRoofVertices(RoofBase roof)
        {
            var vertices = new List<XYZ>();
            try
            {
                if (roof is RoofBase roofBase)
                {
                    var opt = new Options { ComputeReferences = true, DetailLevel = ViewDetailLevel.Medium };
                    var geomElem = roofBase.get_Geometry(opt);

                    foreach (GeometryObject geomObj in geomElem)
                    {
                        if (geomObj is Solid solid)
                        {
                            foreach (Face face in solid.Faces)
                            {
                                var edgeLoops = face.EdgeLoops;
                                if (edgeLoops != null && edgeLoops.Size > 0)
                                {
                                    var outerLoop = edgeLoops.get_Item(0);
                                    foreach (Edge edge in outerLoop)
                                    {
                                        var curve = edge.AsCurve();
                                        if (curve != null)
                                        {
                                            vertices.Add(curve.GetEndPoint(0));
                                            vertices.Add(curve.GetEndPoint(1));
                                        }
                                    }
                                    break;
                                }
                            }
                            break;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _loggerService.LogWarning($"Could not get roof vertices: {ex.Message}");
            }

            var distinctVertices = vertices
                .GroupBy(v => new { X = Math.Round(v.X, 6), Y = Math.Round(v.Y, 6), Z = Math.Round(v.Z, 6) })
                .Select(g => g.First())
                .ToList();

            return distinctVertices;
        }

        private void CreateSampleDrainData()
        {
            var sampleDrains = new List<DrainItem>
            {
                new DrainItem
                {
                    DrainId = 1,
                    ShapeType = "Rectangle",
                    SizeCategory = "150x150",
                    Width = 150,
                    Height = 150,
                    CenterPoint = new Point3D(1000, 2000, 0),
                    IsSelected = true,
                    DrainVertices = new List<Point3D>()
                },
                new DrainItem
                {
                    DrainId = 2,
                    ShapeType = "Circle",
                    SizeCategory = "200",
                    Width = 200,
                    Height = 200,
                    CenterPoint = new Point3D(3000, 1500, 0),
                    IsSelected = true,
                    DrainVertices = new List<Point3D>()
                },
                new DrainItem
                {
                    DrainId = 3,
                    ShapeType = "Square",
                    SizeCategory = "100x100",
                    Width = 100,
                    Height = 100,
                    CenterPoint = new Point3D(2000, 3000, 0),
                    IsSelected = false,
                    DrainVertices = new List<Point3D>()
                },
                new DrainItem
                {
                    DrainId = 4,
                    ShapeType = "Rectangle",
                    SizeCategory = "250x150",
                    Width = 250,
                    Height = 150,
                    CenterPoint = new Point3D(4000, 2500, 0),
                    IsSelected = true,
                    DrainVertices = new List<Point3D>()
                },
                new DrainItem
                {
                    DrainId = 5,
                    ShapeType = "Circle",
                    SizeCategory = "300",
                    Width = 300,
                    Height = 300,
                    CenterPoint = new Point3D(1500, 3500, 0),
                    IsSelected = false,
                    DrainVertices = new List<Point3D>()
                }
            };

            PopulateDrainGrid(sampleDrains);
            _loggerService.LogInfo("Sample drain data created for testing UI.");
        }

        private void PopulateDrainGrid(IEnumerable<DrainItem> drains)
        {
            Application.Current?.Dispatcher.Invoke(() =>
            {
                AllDrains.Clear();
                SizeFilters.Clear();
                SizeFilters.Add("All");

                int drainId = 1;
                foreach (var d in drains)
                {
                    d.DrainId = drainId++;
                    AllDrains.Add(d);
                    int vc = d.DrainVertices?.Count ?? 0;
                    _loggerService.LogInfo($"Found opening: {d.SizeCategory} ({d.ShapeType}) · {vc} vertices");
                }

                var categories = AllDrains
                    .Select(d => d.SizeCategory)
                    .Distinct()
                    .OrderBy(s => s);

                foreach (var cat in categories)
                    SizeFilters.Add(cat);

                PickedDrainCount = AllDrains.Count;
                FinalDrainCount = AllDrains.Count;
                UpdateSelectedCount();

                _loggerService.LogInfo($"✓ {AllDrains.Count} drains detected.");
            });
        }

        // ══════════════════════════════════════════════════════════
        //  COMMAND IMPLEMENTATIONS
        // ══════════════════════════════════════════════════════════

        private bool CanRun() => !SlopeApplied && !HasRun && AllDrains.Any(d => d.IsSelected);
        private bool CanExport() => ExportToExcel && SlopeApplied && !string.IsNullOrEmpty(ExportFolderPath);

        private void ExecuteRun()
        {
            try
            {
                if (!double.TryParse(SlopeInput, out double slope) || slope <= 0)
                {
                    _loggerService.LogError("Invalid slope value. Enter a positive number.");
                    return;
                }

                var selected = AllDrains.Where(d => d.IsSelected).ToList();
                if (!selected.Any())
                {
                    _loggerService.LogWarning("No drains selected.");
                    return;
                }

                HasRun = true;
                StatusMessage = "Processing…";
                _loggerService.LogInfo($"Running AutoSlope at {slope}% on {selected.Count} drains…");

                var sw = System.Diagnostics.Stopwatch.StartNew();

                // Get the roof element
                var roof = _uidoc.Document.GetElement(_roofId) as RoofBase;
                if (roof == null)
                {
                    _loggerService.LogError("Roof not found.");
                    HasRun = false;
                    StatusMessage = "Error";
                    return;
                }

                // Store for later export
                _currentRoofElement = roof;

                // Process slopes using service
                var result = _slopeProcessorService.ProcessRoofSlopes(
                    roof, selected, slope, ThresholdMeters, _loggerService.Log);

                sw.Stop();
                RunDuration_sec = Math.Round(sw.Elapsed.TotalSeconds, 1);

                // Store result for export
                _lastSlopeResult = result;

                // Update metrics from result
                LongestPath_m = result.LongestPathMeters;
                HighestElevation_mm = result.HighestElevationMm;
                AvgSlopePercent = result.AvgSlopePercent;
                VerticesProcessed = result.VerticesProcessed;
                VerticesSkipped = result.VerticesSkipped;
                Percentage2Applied = result.SlopePercent2;
                SlopePercent = slope;   // Ensure it's set

                HasRun = false;
                IsComplete = true;
                SlopeApplied = true;
                StatusMessage = result.Success ? "Complete" : "Error";

                _loggerService.LogInfo($"✓ Run complete in {RunDuration_sec} s");
                _loggerService.LogInfo("🔒 Run button locked — re-open to adjust.");

                ResultsInfo = $"Done · {VerticesProcessed} vertices modified · {LongestPath_m:F2} m longest path";

                // Auto-export if enabled
                if (ExportToExcel && result.Success)
                    ExecuteExport();
            }
            catch (Exception ex)
            {
                HasRun = false;
                StatusMessage = "Error";
                _loggerService.LogError($"Run failed: {ex.Message}");
            }
        }

        private void ExecuteExport()
        {
            try
            {
                if (!ExportToExcel)
                {
                    _loggerService.LogInfo("Excel export is disabled.");
                    return;
                }

                if (!Directory.Exists(ExportFolderPath))
                    Directory.CreateDirectory(ExportFolderPath);

                _loggerService.LogInfo("📊 Exporting to Excel…");
                _loggerService.LogInfo($"   Folder: {ExportFolderPath}");
                _loggerService.LogInfo($"   Detailed: {IncludeVertexDetails}");

                // Use the stored result if available, otherwise create from current metrics
                var result = _lastSlopeResult ?? new SlopeResult
                {
                    LongestPathMeters = LongestPath_m,
                    HighestElevationMm = HighestElevation_mm,
                    AvgSlopePercent = AvgSlopePercent,
                    VerticesProcessed = VerticesProcessed,
                    VerticesSkipped = VerticesSkipped,
                    SlopePercent1 = SlopePercent,
                    SlopePercent2 = Percentage2Applied,
                    Success = true,
                    RunDate = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")
                };

                var roofName = _currentRoofElement?.Name ?? _currentRoof?.Name ?? "Unknown";

                string file = _excelExportService.ExportToExcel(
                    ExportFolderPath, result, AllDrains.ToList(), IncludeVertexDetails, roofName);

                _loggerService.LogInfo($"✓ Saved: {Path.GetFileName(file)}");

                var open = _dialogService.Confirm(
                    "Export complete.\n\nOpen the export folder?", "Export Complete");

                if (open)
                    System.Diagnostics.Process.Start("explorer.exe", ExportFolderPath);
            }
            catch (Exception ex)
            {
                _loggerService.LogError($"Export failed: {ex.Message}");
            }
        }

        private void ExecuteBrowseFolder()
        {
            try
            {
                string selected = _dialogService.SelectFolder(ExportFolderPath);
                if (!string.IsNullOrEmpty(selected))
                {
                    ExportFolderPath = selected;
                    if (!Directory.Exists(ExportFolderPath))
                        Directory.CreateDirectory(ExportFolderPath);
                }
            }
            catch (Exception ex)
            {
                _loggerService.LogError($"Folder browse error: {ex.Message}");
            }
        }

        private void ExecuteCopyLog()
        {
            try
            {
                string logText = _loggerService.GetLogText();
                if (!string.IsNullOrEmpty(logText))
                {
                    Clipboard.SetText(logText);
                    _loggerService.Log("✓ Log copied to clipboard.");
                }
            }
            catch (Exception ex)
            {
                _loggerService.LogError($"Copy failed: {ex.Message}");
            }
        }

        // ══════════════════════════════════════════════════════════
        //  DRAIN SELECTION HELPERS
        // ══════════════════════════════════════════════════════════

        private void SelectAll()
        {
            foreach (var d in AllDrains) d.IsSelected = true;
            PostSelectionRefresh("All");
        }

        private void SelectNone()
        {
            foreach (var d in AllDrains) d.IsSelected = false;
            PostSelectionRefresh("None");
        }

        private void InvertSelection()
        {
            foreach (var d in AllDrains) d.IsSelected = !d.IsSelected;
            PostSelectionRefresh("Inverted");
        }

        private void PostSelectionRefresh(string verb)
        {
            FilteredDrainsView.Refresh();
            UpdateSelectedCount();
            ((RelayCommand)RunCommand).RaiseCanExecuteChanged();
            ((RelayCommand)ExportResultsCommand).RaiseCanExecuteChanged();
            _loggerService.Log($"Selection {verb.ToLower()}: {SelectedDrainsCount} drains selected.");
        }

        // ══════════════════════════════════════════════════════════
        //  FILTER PREDICATE
        // ══════════════════════════════════════════════════════════

        private bool FilterDrainItem(object obj)
        {
            if (obj is DrainItem d)
                return SelectedSizeFilter == "All" || d.SizeCategory == SelectedSizeFilter;
            return false;
        }

        private void UpdateSelectedCount()
        {
            SelectedDrainsCount = FilteredDrainsView
                .Cast<DrainItem>()
                .Count(d => d.IsSelected);
        }
    }
}