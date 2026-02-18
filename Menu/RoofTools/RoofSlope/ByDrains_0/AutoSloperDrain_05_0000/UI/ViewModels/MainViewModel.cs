using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using CommunityToolkit.Mvvm.Input;
using Revit26_Plugin.AutoSlope.V5_00.Core.Engine;
using Revit26_Plugin.AutoSlope.V5_00.Core.Models;
using Revit26_Plugin.AutoSlope.V5_00.Infrastructure.ExternalEvents;
using Revit26_Plugin.AutoSlope.V5_00.Infrastructure.Helpers;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Data;
using System.Windows.Threading;

namespace Revit26_Plugin.AutoSlope.V5_00.UI.ViewModels
{
    public class MainViewModel : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;
        private void Raise([CallerMemberName] string name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

        // Commands
        public IRelayCommand RunCommand { get; }
        public IRelayCommand SelectAllCommand { get; }
        public IRelayCommand SelectNoneCommand { get; }
        public IRelayCommand InvertSelectionCommand { get; }
        public IRelayCommand BrowseFolderCommand { get; }
        public IRelayCommand ClearLogCommand { get; }
        public IRelayCommand ExportResultsCommand { get; }
        public IRelayCommand ChangeRoofCommand { get; }

        // Properties
        private Core.Models.RoofData _currentRoof;
        private string _selectedSizeFilter = "All";
        private bool _hasRun;
        private double _slopePercent = 1.5;
        private int _thresholdMeters = 50;
        private string _exportFolderPath;
        private bool _exportToCsv = true;
        private bool _exportToExcel = true;
        private bool _includeVertexDetails = true;

        public ObservableCollection<DrainItem> AllDrains { get; } = new();
        public ObservableCollection<string> SizeFilters { get; } = new();
        public ObservableCollection<LogEntry> LogEntries { get; } = new();
        public ICollectionView FilteredDrainsView { get; }

        public List<double> SlopeOptions { get; } = new() { 0.5, 1.0, 1.5, 2.0, 2.5, 3.0, 4.0, 5.0 };

        public UIDocument UIDoc { get; }
        public UIApplication App { get; }
        public ElementId RoofId { get; }
        public Action CloseWindow { get; set; }

        // Results
        private int _verticesProcessed;
        private int _verticesSkipped;
        private double _highestElevation_mm;
        private double _longestPath_m;
        private int _runDuration_sec;
        private string _runDate;
        private string _roofInfo;
        private bool _isProcessing;

        public int VerticesProcessed
        {
            get => _verticesProcessed;
            set { _verticesProcessed = value; Raise(); Raise(nameof(SummaryText)); }
        }

        public int VerticesSkipped
        {
            get => _verticesSkipped;
            set { _verticesSkipped = value; Raise(); Raise(nameof(SummaryText)); }
        }

        public double HighestElevation_mm
        {
            get => _highestElevation_mm;
            set { _highestElevation_mm = value; Raise(); Raise(nameof(HighestElevationDisplay)); }
        }
        public string HighestElevationDisplay => $"{HighestElevation_mm:0} mm";

        public double LongestPath_m
        {
            get => _longestPath_m;
            set { _longestPath_m = value; Raise(); Raise(nameof(LongestPathDisplay)); }
        }
        public string LongestPathDisplay => $"{LongestPath_m:0.00} m";

        public int RunDuration_sec
        {
            get => _runDuration_sec;
            set { _runDuration_sec = value; Raise(); Raise(nameof(RunDurationDisplay)); }
        }
        public string RunDurationDisplay => $"{RunDuration_sec} sec";

        public string RunDate
        {
            get => _runDate;
            set { _runDate = value; Raise(); Raise(nameof(SummaryText)); }
        }

        public string RoofInfo
        {
            get => _roofInfo;
            set { _roofInfo = value; Raise(); }
        }

        public double SlopePercent
        {
            get => _slopePercent;
            set { _slopePercent = value; Raise(); }
        }

        public int ThresholdMeters
        {
            get => _thresholdMeters;
            set { _thresholdMeters = value; Raise(); }
        }

        public string ExportFolderPath
        {
            get => _exportFolderPath;
            set { _exportFolderPath = value; Raise(); }
        }

        public bool ExportToCsv
        {
            get => _exportToCsv;
            set { _exportToCsv = value; Raise(); }
        }

        public bool ExportToExcel
        {
            get => _exportToExcel;
            set { _exportToExcel = value; Raise(); }
        }

        public bool IncludeVertexDetails
        {
            get => _includeVertexDetails;
            set { _includeVertexDetails = value; Raise(); }
        }

        public string SelectedSizeFilter
        {
            get => _selectedSizeFilter;
            set
            {
                _selectedSizeFilter = value;
                Raise();
                FilteredDrainsView?.Refresh();
                UpdateSelectedCount();
            }
        }

        public bool HasRun
        {
            get => _hasRun;
            set
            {
                _hasRun = value;
                Raise();
                RunCommand?.NotifyCanExecuteChanged();
                ExportResultsCommand?.NotifyCanExecuteChanged();
            }
        }

        public bool IsProcessing
        {
            get => _isProcessing;
            set
            {
                _isProcessing = value;
                Raise();
                RunCommand?.NotifyCanExecuteChanged();
            }
        }

        private int _selectedDrainsCount;
        public int SelectedDrainsCount
        {
            get => _selectedDrainsCount;
            set { _selectedDrainsCount = value; Raise(); }
        }

        private int _totalDrainsCount;
        public int TotalDrainsCount
        {
            get => _totalDrainsCount;
            set { _totalDrainsCount = value; Raise(); }
        }

        public string SummaryText => $@"
═══════════════════════════════════════
AUTO SLOPE V5 - RESULTS SUMMARY
═══════════════════════════════════════

📊 PROCESSING STATISTICS
─────────────────────────
✓ Vertices Processed  : {VerticesProcessed}
⚠ Vertices Skipped    : {VerticesSkipped}
🔢 Selected Drains    : {SelectedDrainsCount} / {TotalDrainsCount}

📈 SLOPE METRICS
─────────────────────────
📏 Highest Elevation  : {HighestElevationDisplay}
🛤️ Longest Path       : {LongestPathDisplay}
📐 Slope Percentage   : {SlopePercent}%

⏱️ EXECUTION DETAILS
─────────────────────────
⚡ Run Duration       : {RunDurationDisplay}
📅 Run Date           : {RunDate}
📁 Export Folder      : {ExportFolderPath}
═══════════════════════════════════════";

        public MainViewModel(UIDocument uidoc, UIApplication app, ElementId roofId, Action<string> log)
        {
            UIDoc = uidoc;
            App = app;
            RoofId = roofId;

            // Initialize commands with CommunityToolkit
            RunCommand = new RelayCommand(RunAutoSlope, () => !HasRun && !IsProcessing);
            SelectAllCommand = new RelayCommand(SelectAllDrains);
            SelectNoneCommand = new RelayCommand(SelectNoneDrains);
            InvertSelectionCommand = new RelayCommand(InvertDrainSelection);
            BrowseFolderCommand = new RelayCommand(BrowseForFolder);
            ClearLogCommand = new RelayCommand(ClearLog);
            ExportResultsCommand = new RelayCommand(ExportResults, () => HasRun);
            ChangeRoofCommand = new RelayCommand(ChangeRoof);

            // Setup filtered view
            FilteredDrainsView = CollectionViewSource.GetDefaultView(AllDrains);
            FilteredDrainsView.Filter = FilterDrainItem;

            // Set default export folder
            ExportFolderPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                "AutoSlope_V5_Reports");

            // Initialize with roof
            InitializeRoofData();
        }

        private void InitializeRoofData()
        {
            try
            {
                AddLog(LogColorHelper.Cyan("═══════════════════════════════════════"));
                AddLog(LogColorHelper.Cyan("     AUTO SLOPE V5 - INITIALIZATION"));
                AddLog(LogColorHelper.Cyan("═══════════════════════════════════════"));

                Document doc = UIDoc.Document;
                RoofBase roof = doc.GetElement(RoofId) as RoofBase;

                if (roof == null)
                {
                    AddLog(LogColorHelper.Red("✗ ERROR: Could not load roof element"));
                    return;
                }

                RoofInfo = $"{roof.Name} (ID: {RoofId})";
                AddLog(LogColorHelper.Green($"✓ Roof loaded: {roof.Name}"));

                _currentRoof = new Core.Models.RoofData { Roof = roof };

                // Get top face
                AddLog(LogColorHelper.Yellow("🔍 Analyzing roof geometry..."));
                _currentRoof.TopFace = Core.Engine.GeometryHelper.GetTopFace(roof);
                if (_currentRoof.TopFace == null)
                {
                    AddLog(LogColorHelper.Red("✗ Could not find top face of roof"));
                    return;
                }
                AddLog(LogColorHelper.Green("✓ Top face detected"));

                // Get vertices
                var editor = roof.GetSlabShapeEditor();
                if (editor != null && editor.IsEnabled)
                {
                    foreach (SlabShapeVertex vertex in editor.SlabShapeVertices)
                    {
                        if (vertex != null)
                            _currentRoof.Vertices.Add(vertex);
                    }
                }
                AddLog(LogColorHelper.Cyan($"ℹ Found {_currentRoof.Vertices.Count} vertices on roof"));

                // Detect drains
                AddLog(LogColorHelper.Yellow("🔍 Scanning for drain openings..."));
                var drainService = new DrainDetectionService();
                var detectedDrains = drainService.DetectDrainsFromRoof(roof, _currentRoof.TopFace);

                foreach (var drain in detectedDrains)
                {
                    if (drain != null)
                        AllDrains.Add(drain);
                }

                TotalDrainsCount = AllDrains.Count;

                // Update filters
                UpdateSizeFilters();

                if (detectedDrains.Count > 0)
                {
                    AddLog(LogColorHelper.Green($"✓ Found {detectedDrains.Count} drain openings"));
                    foreach (var drain in detectedDrains.Take(5)) // Show first 5 drains
                    {
                        AddLog(LogColorHelper.Cyan($"  • {drain.SizeCategory} - {drain.ShapeType} at ({drain.CenterPoint.X:F1}, {drain.CenterPoint.Y:F1})"));
                    }
                    if (detectedDrains.Count > 5)
                    {
                        AddLog(LogColorHelper.Cyan($"  • ... and {detectedDrains.Count - 5} more"));
                    }
                }
                else
                {
                    AddLog(LogColorHelper.Yellow("⚠ No drain openings detected on this roof"));
                }

                UpdateSelectedCount();
                AddLog(LogColorHelper.Green("✓ Initialization complete"));
            }
            catch (Exception ex)
            {
                AddLog(LogColorHelper.Red($"✗ Initialization error: {ex.Message}"));
            }
        }

        private void UpdateSizeFilters()
        {
            SizeFilters.Clear();
            SizeFilters.Add("All");

            var categories = AllDrains
                .Select(d => d.SizeCategory)
                .Distinct()
                .OrderBy(s => s);

            foreach (var cat in categories)
                SizeFilters.Add(cat);

            SizeFilters.Add("Less than 100x100");
            SizeFilters.Add("100x100 - 200x200");
            SizeFilters.Add("200x200 - 300x300");
            SizeFilters.Add("Greater than 300x300");
        }

        private bool FilterDrainItem(object obj)
        {
            if (obj is not DrainItem drain) return false;
            if (SelectedSizeFilter == "All") return true;

            return SelectedSizeFilter switch
            {
                "Less than 100x100" => drain.Width < 100 && drain.Height < 100,
                "100x100 - 200x200" => drain.Width >= 100 && drain.Width <= 200 && drain.Height >= 100 && drain.Height <= 200,
                "200x200 - 300x300" => drain.Width > 200 && drain.Width <= 300 && drain.Height > 200 && drain.Height <= 300,
                "Greater than 300x300" => drain.Width > 300 && drain.Height > 300,
                _ => drain.SizeCategory == SelectedSizeFilter
            };
        }

        private void RunAutoSlope()
        {
            if (HasRun || IsProcessing) return;

            // Check if event manager is initialized
            if (!AutoSlopeEventManager.IsInitialized)
            {
                AddLog(LogColorHelper.Red("✗ Event manager not initialized. Please restart the command."));
                return;
            }

            IsProcessing = true;
            HasRun = true;
            LogEntries.Clear();

            AddLog(LogColorHelper.Green("═══════════════════════════════════════"));
            AddLog(LogColorHelper.Green("     AUTO SLOPE V5 - PROCESSING"));
            AddLog(LogColorHelper.Green("═══════════════════════════════════════"));
            AddLog(LogColorHelper.Cyan($"Slope: {SlopePercent}% | Threshold: {ThresholdMeters}m"));

            var selectedDrains = AllDrains.Where(d => d.IsSelected).ToList();
            if (selectedDrains.Count == 0)
            {
                AddLog(LogColorHelper.Red("✗ No drains selected"));
                HasRun = false;
                IsProcessing = false;
                return;
            }

            AddLog(LogColorHelper.Cyan($"Processing {selectedDrains.Count} selected drains..."));

            // Create export directory if needed
            if ((ExportToCsv || ExportToExcel) && !Directory.Exists(ExportFolderPath))
            {
                try
                {
                    Directory.CreateDirectory(ExportFolderPath);
                    AddLog(LogColorHelper.Green($"✓ Created export directory: {ExportFolderPath}"));
                }
                catch (Exception ex)
                {
                    AddLog(LogColorHelper.Red($"✗ Failed to create export directory: {ex.Message}"));
                }
            }

            // Create payload and raise event
            var payload = new AutoSlopePayload
            {
                RoofId = RoofId,
                RoofData = _currentRoof,
                SelectedDrains = selectedDrains,
                SlopePercent = SlopePercent,
                ThresholdMeters = ThresholdMeters,
                Vm = this,
                Log = (msg) => AddLog(msg),
                ExportConfig = new ExportConfig
                {
                    ExportPath = ExportFolderPath,
                    ExportToCsv = ExportToCsv,
                    IncludeVertexDetails = IncludeVertexDetails,
                    FileNamePrefix = "AutoSlope_V5"
                }
            };

            try
            {
                AutoSlopeEventManager.RaiseEvent(payload);
            }
            catch (Exception ex)
            {
                AddLog(LogColorHelper.Red($"✗ Failed to start processing: {ex.Message}"));
                HasRun = false;
                IsProcessing = false;
            }
        }

        private void SelectAllDrains()
        {
            foreach (var drain in AllDrains)
                drain.IsSelected = true;

            FilteredDrainsView?.Refresh();
            UpdateSelectedCount();
            AddLog(LogColorHelper.Cyan("✓ All drains selected"));
        }

        private void SelectNoneDrains()
        {
            foreach (var drain in AllDrains)
                drain.IsSelected = false;

            FilteredDrainsView?.Refresh();
            UpdateSelectedCount();
            AddLog(LogColorHelper.Cyan("✓ All drains deselected"));
        }

        private void InvertDrainSelection()
        {
            foreach (var drain in AllDrains)
                drain.IsSelected = !drain.IsSelected;

            FilteredDrainsView?.Refresh();
            UpdateSelectedCount();
            AddLog(LogColorHelper.Cyan("✓ Selection inverted"));
        }

        private void UpdateSelectedCount()
        {
            int count = 0;
            if (FilteredDrainsView != null)
            {
                foreach (DrainItem drain in FilteredDrainsView)
                {
                    if (drain.IsSelected)
                        count++;
                }
            }
            SelectedDrainsCount = count;
        }

        private void BrowseForFolder()
        {
            try
            {
                var selectedPath = DialogService.SelectFolder(ExportFolderPath);
                if (!string.IsNullOrEmpty(selectedPath))
                {
                    ExportFolderPath = selectedPath;
                    AddLog(LogColorHelper.Green($"✓ Export folder set to: {ExportFolderPath}"));
                }
            }
            catch (Exception ex)
            {
                AddLog(LogColorHelper.Red($"✗ Failed to browse folder: {ex.Message}"));
            }
        }

        private void ClearLog()
        {
            LogEntries.Clear();
            AddLog(LogColorHelper.Cyan("✓ Log cleared"));
        }

        private void ExportResults()
        {
            if (!HasRun)
            {
                AddLog(LogColorHelper.Yellow("⚠ Run AutoSlope first to export results"));
                return;
            }

            try
            {
                var filePath = DialogService.ShowSaveFileDialog(
                    "CSV files (*.csv)|*.csv|Excel files (*.xlsx)|*.xlsx|All files (*.*)|*.*",
                    ExportFolderPath,
                    $"AutoSlope_V5_Results_{DateTime.Now:yyyyMMdd_HHmmss}.csv");

                if (!string.IsNullOrEmpty(filePath))
                {
                    // Simple summary export
                    var exportData = new List<string>
                    {
                        "AutoSlope V5 Results Export",
                        $"Export Date: {DateTime.Now:yyyy-MM-dd HH:mm:ss}",
                        "",
                        "SUMMARY",
                        $"Vertices Processed,{VerticesProcessed}",
                        $"Vertices Skipped,{VerticesSkipped}",
                        $"Selected Drains,{SelectedDrainsCount}",
                        $"Total Drains,{TotalDrainsCount}",
                        $"Highest Elevation (mm),{HighestElevation_mm:0}",
                        $"Longest Path (m),{LongestPath_m:0.00}",
                        $"Run Duration (sec),{RunDuration_sec}",
                        $"Run Date,{RunDate}",
                        $"Slope Percentage,{SlopePercent}",
                        $"Threshold (m),{ThresholdMeters}",
                        $"Export Folder,{ExportFolderPath}"
                    };

                    File.WriteAllLines(filePath, exportData);
                    AddLog(LogColorHelper.Green($"✓ Results exported to: {filePath}"));
                }
            }
            catch (Exception ex)
            {
                AddLog(LogColorHelper.Red($"✗ Export error: {ex.Message}"));
            }
        }

        private void ChangeRoof()
        {
            CloseWindow?.Invoke();
        }

        public void AddLog(string message)
        {
            try
            {
                Application.Current?.Dispatcher?.BeginInvoke(new Action(() =>
                {
                    if (message != null)
                    {
                        LogEntries.Add(new LogEntry(message));

                        // Keep last 1000 entries
                        while (LogEntries.Count > 1000)
                            LogEntries.RemoveAt(0);
                    }
                }));
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to add log: {ex.Message}");
            }
        }

        public void ProcessingComplete()
        {
            Application.Current?.Dispatcher?.BeginInvoke(new Action(() =>
            {
                IsProcessing = false;
                AddLog(LogColorHelper.Green("✅ Processing complete!"));
            }));
        }
    }

    public class LogEntry
    {
        public string FullMessage { get; }
        public string Message { get; }
        public string Color { get; }
        public string Timestamp { get; }

        public LogEntry(string formattedMessage)
        {
            FullMessage = formattedMessage ?? string.Empty;

            // Parse color from format: <color=#HEX> [HH:MM:SS] message</color>
            if (!string.IsNullOrEmpty(formattedMessage) && formattedMessage.StartsWith("<color="))
            {
                try
                {
                    int colorStart = 7;
                    int colorEnd = formattedMessage.IndexOf('>');
                    if (colorEnd > colorStart)
                    {
                        Color = formattedMessage.Substring(colorStart, colorEnd - colorStart);

                        int messageStart = formattedMessage.IndexOf(']') + 2;
                        int messageEnd = formattedMessage.LastIndexOf('<');
                        if (messageEnd > messageStart && messageStart > 0)
                        {
                            Message = formattedMessage.Substring(messageStart, messageEnd - messageStart);
                            Timestamp = formattedMessage.Substring(colorEnd + 2, 8);
                        }
                        else
                        {
                            Message = formattedMessage;
                            Timestamp = DateTime.Now.ToString("HH:mm:ss");
                        }
                    }
                    else
                    {
                        Color = "#CCCCCC";
                        Message = formattedMessage;
                        Timestamp = DateTime.Now.ToString("HH:mm:ss");
                    }
                }
                catch
                {
                    Color = "#CCCCCC";
                    Message = formattedMessage;
                    Timestamp = DateTime.Now.ToString("HH:mm:ss");
                }
            }
            else
            {
                Color = "#CCCCCC";
                Message = formattedMessage ?? string.Empty;
                Timestamp = DateTime.Now.ToString("HH:mm:ss");
            }
        }
    }
}