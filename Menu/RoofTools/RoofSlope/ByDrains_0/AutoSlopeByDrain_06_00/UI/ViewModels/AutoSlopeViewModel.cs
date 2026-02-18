using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using CommunityToolkit.Mvvm.Input;
using Revit26_Plugin.AutoSlopeByDrain_06_00.Core.Models;
using Revit26_Plugin.AutoSlopeByDrain_06_00.Infrastructure.ExternalEvents;
using Revit26_Plugin.AutoSlopeByDrain_06_00.Infrastructure.Helpers;
using Revit26_Plugin.AutoSlopeByDrain_06_00.Infrastructure.Services;
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

namespace Revit26_Plugin.AutoSlopeByDrain_06_00.UI.ViewModels
{
    public partial class AutoSlopeViewModel : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;
        private void Raise([CallerMemberName] string name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

        // Drain collections
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
                Raise();
                FilteredDrainsView.Refresh();
                UpdateSelectedCount();
                AddLog($"Filter applied: {value}");
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

        private string _roofInfo;
        public string RoofInfo
        {
            get => _roofInfo;
            set { _roofInfo = value; Raise(); }
        }

        // Slope options
        public List<double> SlopeOptions { get; } = new List<double> { 0.5, 1.0, 1.5, 2.0, 2.5, 3.0 };

        private double _slopePercent = 1.5;
        public double SlopePercent
        {
            get => _slopePercent;
            set { _slopePercent = value; Raise(); }
        }

        private string _slopeInput = "1.5";
        public string SlopeInput
        {
            get => _slopeInput;
            set
            {
                _slopeInput = value;
                Raise();
                if (double.TryParse(value, out double result) && result > 0)
                {
                    SlopePercent = result;
                    AddLog($"Slope set to: {result}%");
                }
            }
        }

        private int _thresholdMeters = 50;
        public int ThresholdMeters
        {
            get => _thresholdMeters;
            set { _thresholdMeters = value; Raise(); }
        }

        // Export settings
        private string _exportFolderPath;
        public string ExportFolderPath
        {
            get => _exportFolderPath;
            set { _exportFolderPath = value; Raise(); }
        }

        private bool _exportToCsv = true;
        public bool ExportToCsv
        {
            get => _exportToCsv;
            set { _exportToCsv = value; Raise(); }
        }

        private bool _includeVertexDetails = false;
        public bool IncludeVertexDetails
        {
            get => _includeVertexDetails;
            set { _includeVertexDetails = value; Raise(); }
        }

        // Log and status
        private string _logText = "";
        public string LogText
        {
            get => _logText;
            set { _logText = value; Raise(); }
        }

        public string StatusMessage => HasRun ? "Processing..." : "Ready";
        public string StatusColor => HasRun ? "#E67E22" : "#27AE60";

        // Results
        private int _verticesProcessed;
        public int VerticesProcessed
        {
            get => _verticesProcessed;
            set { _verticesProcessed = value; Raise(); Raise(nameof(SummaryText)); }
        }

        private int _verticesSkipped;
        public int VerticesSkipped
        {
            get => _verticesSkipped;
            set { _verticesSkipped = value; Raise(); Raise(nameof(SummaryText)); }
        }

        private int _drainCount;
        public int DrainCount
        {
            get => _drainCount;
            set { _drainCount = value; Raise(); Raise(nameof(SummaryText)); }
        }

        private double _highestElevation_mm;
        public double HighestElevation_mm
        {
            get => _highestElevation_mm;
            set { _highestElevation_mm = value; Raise(); Raise(nameof(HighestElevationDisplay)); }
        }
        public string HighestElevationDisplay => $"{HighestElevation_mm:0} mm";

        private double _longestPath_m;
        public double LongestPath_m
        {
            get => _longestPath_m;
            set { _longestPath_m = value; Raise(); Raise(nameof(LongestPathDisplay)); }
        }
        public string LongestPathDisplay => $"{LongestPath_m:0.00} m";

        private int _runDuration_sec;
        public int RunDuration_sec
        {
            get => _runDuration_sec;
            set { _runDuration_sec = value; Raise(); Raise(nameof(RunDurationDisplay)); }
        }
        public string RunDurationDisplay => $"{RunDuration_sec} sec";

        private string _runDate;
        public string RunDate
        {
            get => _runDate;
            set { _runDate = value; Raise(); Raise(nameof(SummaryText)); }
        }

        public string SummaryText =>
$@"Vertices Processed : {VerticesProcessed}
Vertices Skipped   : {VerticesSkipped}
Drain Count        : {DrainCount}
Highest Elevation  : {HighestElevationDisplay}
Longest Path       : {LongestPathDisplay}
Run Duration       : {RunDurationDisplay}
Run Date           : {RunDate}
Export Folder      : {ExportFolderPath}";

        private bool _hasRun;
        public bool HasRun
        {
            get => _hasRun;
            set
            {
                _hasRun = value;
                Raise();
                Raise(nameof(StatusMessage));
                Raise(nameof(StatusColor));
                RunCommand.NotifyCanExecuteChanged();
                ExportResultsCommand.NotifyCanExecuteChanged();
            }
        }

        private bool _isProcessing;
        public bool IsProcessing
        {
            get => _isProcessing;
            set
            {
                _isProcessing = value;
                Raise();
                CommandManager.InvalidateRequerySuggested();
            }
        }

        // Commands - Using IRelayCommand from CommunityToolkit
        public IRelayCommand ApplySlopesCommand { get; }
        public IRelayCommand SelectAllCommand { get; }
        public IRelayCommand SelectNoneCommand { get; }
        public IRelayCommand InvertSelectionCommand { get; }
        public IRelayCommand RefreshDrainsCommand { get; }
        public IRelayCommand RunCommand { get; }
        public IRelayCommand BrowseFolderCommand { get; }
        public IRelayCommand ClearLogCommand { get; }
        public IRelayCommand ExportResultsCommand { get; }
        public IRelayCommand ExportLogCommand { get; }
        public IRelayCommand CopyLogCommand { get; }
        public IRelayCommand OkCommand { get; }
        public IRelayCommand CancelCommand { get; }

        public UIDocument UIDoc { get; }
        public UIApplication App { get; }
        public ElementId RoofId { get; }
        private readonly List<DrainItem> _initialDrains;
        private readonly Action<string> _log;
        private readonly DrainDetectionService _drainService;
        private DateTime _operationStartTime;

        public AutoSlopeViewModel(
            UIDocument uidoc,
            UIApplication app,
            ElementId roofId,
            List<DrainItem> detectedDrains,
            Action<string> log)
        {
            UIDoc = uidoc;
            App = app;
            RoofId = roofId;
            _initialDrains = detectedDrains;
            _log = log;
            _drainService = new DrainDetectionService();
            _operationStartTime = DateTime.Now;

            // Initialize collections
            foreach (var drain in detectedDrains)
                AllDrains.Add(drain);

            TotalDrainsCount = detectedDrains.Count;
            UpdateSelectedCount();

            // Setup filtered view
            FilteredDrainsView = CollectionViewSource.GetDefaultView(AllDrains);
            FilteredDrainsView.Filter = FilterDrainItem;

            // Generate size filters
            var categories = _drainService.GenerateSizeCategories(detectedDrains);
            foreach (var category in categories)
                SizeFilters.Add(category);

            // Set roof info
            var roof = doc.GetElement(roofId) as RoofBase;
            RoofInfo = roof != null ? $"{roof.Name} (Id: {roof.Id})" : "Unknown Roof";

            // Export folder default
            ExportFolderPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                "AutoSlope_Reports");

            // Initialize commands with CommunityToolkit
            ApplySlopesCommand = new RelayCommand(ApplySlopes, () => CanApplySlopes());
            SelectAllCommand = new RelayCommand(SelectAllDrains, () => !IsProcessing);
            SelectNoneCommand = new RelayCommand(SelectNoneDrains, () => !IsProcessing);
            InvertSelectionCommand = new RelayCommand(InvertDrainSelection, () => !IsProcessing);
            RefreshDrainsCommand = new RelayCommand(RefreshDrains, () => !IsProcessing);
            RunCommand = new RelayCommand(RunAutoSlope, () => !HasRun && !IsProcessing);
            BrowseFolderCommand = new RelayCommand(BrowseForFolder);
            ClearLogCommand = new RelayCommand(ClearLog);
            ExportResultsCommand = new RelayCommand(ExportResults, () => HasRun);
            ExportLogCommand = new RelayCommand(ExportLog, () => !string.IsNullOrEmpty(LogText));
            CopyLogCommand = new RelayCommand(CopyLogToClipboard, () => !string.IsNullOrEmpty(LogText));
            OkCommand = new RelayCommand(OkOperation);
            CancelCommand = new RelayCommand(CancelOperation);

            // Initial log
            AddLog("═══════════════════════════════════════════");
            AddLog("   AUTO ROOF SLOPE BY DRAIN v6.0");
            AddLog("═══════════════════════════════════════════");
            AddLog($"Detected {detectedDrains.Count} drain openings");
            AddLog("Select drains to use for slope calculation");
            AddLog("");
        }

        private Document doc => UIDoc.Document;

        private bool FilterDrainItem(object obj)
        {
            var drain = obj as DrainItem;
            if (drain == null) return false;
            if (SelectedSizeFilter == "All") return true;

            var filtered = _drainService.FilterDrainsBySize(new List<DrainItem> { drain }, SelectedSizeFilter);
            return filtered.Any();
        }

        private void UpdateSelectedCount()
        {
            int count = 0;
            foreach (var drain in FilteredDrainsView.Cast<DrainItem>())
            {
                if (drain.IsSelected)
                    count++;
            }
            SelectedDrainsCount = count;
        }

        private void SelectAllDrains()
        {
            foreach (var drain in AllDrains)
                drain.IsSelected = true;

            FilteredDrainsView.Refresh();
            UpdateSelectedCount();
            AddLog($"✓ Selected all {AllDrains.Count} drains");
        }

        private void SelectNoneDrains()
        {
            foreach (var drain in AllDrains)
                drain.IsSelected = false;

            FilteredDrainsView.Refresh();
            UpdateSelectedCount();
            AddLog("✓ All drains deselected");
        }

        private void InvertDrainSelection()
        {
            foreach (var drain in AllDrains)
                drain.IsSelected = !drain.IsSelected;

            FilteredDrainsView.Refresh();
            UpdateSelectedCount();
            AddLog($"✓ Selection inverted - {SelectedDrainsCount} drains now selected");
        }

        private void RefreshDrains()
        {
            try
            {
                IsProcessing = true;
                AddLog("🔄 Refreshing drain detection...");

                var roof = doc.GetElement(RoofId) as RoofBase;
                if (roof == null) return;

                var topFace = GetTopFace(roof);
                var refreshedDrains = _drainService.DetectDrainsFromRoof(roof, topFace);

                AllDrains.Clear();
                foreach (var drain in refreshedDrains)
                    AllDrains.Add(drain);

                TotalDrainsCount = refreshedDrains.Count;
                UpdateSelectedCount();

                // Refresh filters
                SizeFilters.Clear();
                var categories = _drainService.GenerateSizeCategories(refreshedDrains);
                foreach (var category in categories)
                    SizeFilters.Add(category);

                AddLog($"✅ Refresh complete. Found {refreshedDrains.Count} drains.");
            }
            catch (Exception ex)
            {
                AddLog($"❌ Error refreshing drains: {ex.Message}");
            }
            finally
            {
                IsProcessing = false;
            }
        }

        private bool CanApplySlopes()
        {
            return AllDrains.Any(d => d.IsSelected) && !IsProcessing && !HasRun;
        }

        private void ApplySlopes()
        {
            if (!double.TryParse(SlopeInput, out double slope) || slope <= 0)
            {
                AddLog("❌ Please enter a valid positive slope percentage");
                return;
            }

            var selectedDrains = AllDrains.Where(d => d.IsSelected).ToList();
            if (selectedDrains.Count == 0)
            {
                AddLog("❌ No drains selected");
                return;
            }

            // Prepare payload with selected drains
            var payload = new AutoSlopePayload
            {
                RoofId = RoofId,
                DrainPoints = selectedDrains.Select(d => d.CenterPoint).ToList(),
                DrainItems = selectedDrains,
                SlopePercent = slope,
                ThresholdMeters = ThresholdMeters,
                Vm = this,
                Log = AddLog,
                ExportConfig = new ExportConfig
                {
                    ExportPath = ExportFolderPath,
                    ExportToCsv = ExportToCsv,
                    IncludeVertexDetails = IncludeVertexDetails
                }
            };

            AutoSlopeHandler.Payload = payload;
            AutoSlopeEventManager.Event.Raise();
            HasRun = true;
        }

        private void RunAutoSlope()
        {
            // This is kept for compatibility with existing event system
            ApplySlopes();
        }

        private void BrowseForFolder()
        {
            var selectedPath = DialogService.SelectFolder(ExportFolderPath);
            if (!string.IsNullOrEmpty(selectedPath))
            {
                ExportFolderPath = selectedPath;
                AddLog($"Export folder set to: {ExportFolderPath}");
            }
        }

        private void ClearLog()
        {
            LogText = "";
            AddLog("Log cleared.");
        }

        private void ExportResults()
        {
            // Implementation from Part 01 - keep existing
            AddLog("Exporting results...");
            // Add your export logic here
        }

        private void ExportLog()
        {
            try
            {
                var dialog = new Microsoft.Win32.SaveFileDialog
                {
                    Filter = "Text Files (*.txt)|*.txt|All Files (*.*)|*.*",
                    DefaultExt = "txt",
                    FileName = $"AutoSlope_Log_{DateTime.Now:yyyyMMdd_HHmmss}.txt"
                };

                if (dialog.ShowDialog() == true)
                {
                    File.WriteAllText(dialog.FileName, LogText);
                    AddLog($"📁 Log exported to: {dialog.FileName}");
                }
            }
            catch (Exception ex)
            {
                AddLog($"❌ Failed to export log: {ex.Message}");
            }
        }

        private void CopyLogToClipboard()
        {
            try
            {
                Clipboard.SetText(LogText);
                AddLog("📋 Log copied to clipboard");
            }
            catch (Exception ex)
            {
                AddLog($"❌ Failed to copy log: {ex.Message}");
            }
        }

        private void OkOperation()
        {
            AddLog("");
            AddLog($"✅ Session completed. Total time: {DateTime.Now - _operationStartTime:mm\\:ss}");
            CloseWindow?.Invoke();
        }

        private void CancelOperation()
        {
            if (IsProcessing)
            {
                var result = MessageBox.Show(
                    "Operation in progress. Cancel?",
                    "Confirm Cancel",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Warning);
                if (result == MessageBoxResult.No) return;
            }

            AddLog("");
            AddLog("⚠️ Operation cancelled by user");
            CloseWindow?.Invoke();
        }

        public Action CloseWindow { get; set; }

        public void AddLog(string message)
        {
            _log?.Invoke(message);
            Application.Current.Dispatcher.BeginInvoke(new Action(() =>
            {
                LogText += $"[{DateTime.Now:HH:mm:ss}] {message}\n";
            }));
        }

        private Face GetTopFace(RoofBase roof)
        {
            Options opt = new Options();
            GeometryElement geom = roof.get_Geometry(opt);
            if (geom == null) return null;

            Face topFace = null;
            double maxZ = double.MinValue;

            foreach (GeometryObject obj in geom)
            {
                if (obj is Solid solid)
                {
                    foreach (Face face in solid.Faces)
                    {
                        BoundingBoxUV bb = face.GetBoundingBox();
                        if (bb == null) continue;

                        UV mid = new UV((bb.Min.U + bb.Max.U) * 0.5, (bb.Min.V + bb.Max.V) * 0.5);
                        XYZ p = face.Evaluate(mid);
                        if (p != null && p.Z > maxZ)
                        {
                            maxZ = p.Z;
                            topFace = face;
                        }
                    }
                }
            }
            return topFace;
        }
    }
}