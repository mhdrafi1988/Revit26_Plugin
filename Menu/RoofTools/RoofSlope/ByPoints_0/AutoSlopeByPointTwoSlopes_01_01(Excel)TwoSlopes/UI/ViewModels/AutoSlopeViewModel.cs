using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using CommunityToolkit.Mvvm.Input;
using OfficeOpenXml;
using OfficeOpenXml.Style;
using AutoSlopeByPointTwoSlopes_01_00.Core.Models;
using AutoSlopeByPointTwoSlopes_01_00.Infrastructure.ExternalEvents;
using AutoSlopeByPointTwoSlopes_01_00.Infrastructure.Helpers;
using AutoSlopeByPointTwoSlopes_01_00.UI.Views;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.IO;
using System.Runtime.CompilerServices;
using System.Windows;
using HelpersLogColorHelper = AutoSlopeByPointTwoSlopes_01_00.Infrastructure.Helpers.LogColorHelper;

namespace AutoSlopeByPointTwoSlopes_01_00.UI.ViewModels
{
    public class AutoSlopeViewModel : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;
        private void Raise([CallerMemberName] string name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

        public List<double> SlopeOptions { get; } = new List<double> { 0.5, 1.0, 1.5, 2.0, 2.5, 3.0, 4.0, 5.0 };

        private double _slopePercent = 1.5;
        public double SlopePercent
        {
            get => _slopePercent;
            set { _slopePercent = value; Raise(); }
        }

        private double _specialSlopePercent = 2.0;
        public double SpecialSlopePercent
        {
            get => _specialSlopePercent;
            set { _specialSlopePercent = value; Raise(); Raise(nameof(SpecialSlopeDisplay)); Raise(nameof(SummaryText)); }
        }

        private double _remainingSlopePercent = 1.0;
        public double RemainingSlopePercent
        {
            get => _remainingSlopePercent;
            set { _remainingSlopePercent = value; Raise(); Raise(nameof(RemainingSlopeDisplay)); Raise(nameof(SummaryText)); }
        }

        public string AppliedSlopeDisplay => $"{SlopePercent}%";
        public string SpecialSlopeDisplay => $"{SpecialSlopePercent}%";
        public string RemainingSlopeDisplay => $"{RemainingSlopePercent}%";

        private int _thresholdMeters = 50;
        public int ThresholdMeters
        {
            get => _thresholdMeters;
            set { _thresholdMeters = value; Raise(); Raise(nameof(SummaryText)); }
        }

        private int _drainToleranceMm = 500;
        public int DrainToleranceMm
        {
            get => _drainToleranceMm;
            set { _drainToleranceMm = value; Raise(); Raise(nameof(SummaryText)); }
        }

        private bool _enableDrainTolerance = false;
        public bool EnableDrainTolerance
        {
            get => _enableDrainTolerance;
            set { _enableDrainTolerance = value; Raise(); Raise(nameof(SummaryText)); }
        }

        private string _exportFolderPath;
        public string ExportFolderPath
        {
            get => _exportFolderPath;
            set { _exportFolderPath = value; Raise(); Raise(nameof(SummaryText)); }
        }

        private bool _exportToExcel = true;
        public bool ExportToExcel
        {
            get => _exportToExcel;
            set { _exportToExcel = value; }
        }

        private bool _includeVertexDetails = true;
        public bool IncludeVertexDetails
        {
            get => _includeVertexDetails;
            set { _includeVertexDetails = value; }
        }

        private string _logText = "";
        public string LogText
        {
            get => _logText;
            set { _logText = value; Raise(); }
        }

        private List<XYZ> _drainPoints;
        public List<XYZ> DrainPoints
        {
            get => _drainPoints;
            set
            {
                _drainPoints = value;
                Raise();
                Raise(nameof(DrainCountDisplay));
                Raise(nameof(CanRun));
                Raise(nameof(SummaryText));
            }
        }

        public string DrainCountDisplay => $"{DrainPoints?.Count ?? 0} drain point(s) selected";

        private HashSet<int> _selectedVertexIndices = new HashSet<int>();
        public HashSet<int> SelectedVertexIndices
        {
            get => _selectedVertexIndices;
            set
            {
                _selectedVertexIndices = value;
                Raise();
                Raise(nameof(SpecialVertexCountDisplay));
                Raise(nameof(CanRun));
                Raise(nameof(SummaryText));
            }
        }

        public int SpecialVertexCount => SelectedVertexIndices?.Count ?? 0;
        public string SpecialVertexCountDisplay => $"{SpecialVertexCount} vertex(ices) selected";

        private bool _isSelectingVertices = false;
        public bool IsSelectingVertices
        {
            get => _isSelectingVertices;
            set { _isSelectingVertices = value; Raise(); Raise(nameof(SelectVerticesButtonText)); }
        }

        public string SelectVerticesButtonText => IsSelectingVertices ? "Cancel Vertex Selection" : "Select Special Vertices";

        public bool CanRun => (SelectedVertexIndices != null && SelectedVertexIndices.Count > 0) && !HasRun;

        public string StatusMessage => HasRun ? "Processing..." : (CanRun ? "Ready to run" : "Select special vertices first");
        public string StatusColor => HasRun ? "#E67E22" : (CanRun ? "#27AE60" : "#95A5A6");

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
            set { _highestElevation_mm = value; Raise(); Raise(nameof(HighestElevationDisplay)); Raise(nameof(SummaryText)); }
        }
        public string HighestElevationDisplay => $"{HighestElevation_mm:0} mm";

        private double _longestPath_m;
        public double LongestPath_m
        {
            get => _longestPath_m;
            set { _longestPath_m = value; Raise(); Raise(nameof(LongestPathDisplay)); Raise(nameof(SummaryText)); }
        }
        public string LongestPathDisplay => $"{LongestPath_m:0.00} m";

        private int _runDuration_sec;
        public int RunDuration_sec
        {
            get => _runDuration_sec;
            set { _runDuration_sec = value; Raise(); Raise(nameof(RunDurationDisplay)); Raise(nameof(SummaryText)); }
        }
        public string RunDurationDisplay => $"{RunDuration_sec} sec";

        private string _runDate;
        public string RunDate
        {
            get => _runDate;
            set { _runDate = value; Raise(); Raise(nameof(SummaryText)); }
        }

        public string SummaryText =>
$@"Special Vertices Slope : {SpecialSlopeDisplay}
Remaining Vertices Slope : {RemainingSlopeDisplay}
Special Vertices : {SpecialVertexCount}
Drain Points : {DrainPoints?.Count ?? 0}
Vertices Processed : {VerticesProcessed}
Vertices Skipped   : {VerticesSkipped}
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

        private RelayCommand _runCommand;
        public RelayCommand RunCommand => _runCommand ??= new RelayCommand(RunAutoSlope, () => CanRun);

        private RelayCommand _browseFolderCommand;
        public RelayCommand BrowseFolderCommand => _browseFolderCommand ??= new RelayCommand(BrowseForFolder);

        private RelayCommand _clearLogCommand;
        public RelayCommand ClearLogCommand => _clearLogCommand ??= new RelayCommand(ClearLog);

        private RelayCommand _exportResultsCommand;
        public RelayCommand ExportResultsCommand => _exportResultsCommand ??= new RelayCommand(ExportResults, () => HasRun);

        private RelayCommand _selectVerticesCommand;
        public RelayCommand SelectVerticesCommand => _selectVerticesCommand ??= new RelayCommand(
            StartVertexSelection, () => !HasRun);

        private RelayCommand _clearSelectionCommand;
        public RelayCommand ClearSelectionCommand => _clearSelectionCommand ??= new RelayCommand(
            ClearVertexSelection, () => !HasRun && SelectedVertexIndices.Count > 0);

        // ─────────────────────────────────────────────────────────────────────
        // FIX: ExternalEvent and handler are injected from AutoSlopeCommand,
        // where they were created inside IExternalCommand.Execute().
        // They must NEVER be created lazily from the WPF UI thread — Revit
        // silently produces a broken event that never fires in that case.
        // ─────────────────────────────────────────────────────────────────────
        private readonly ExternalEvent _vertexSelectionEvent;
        private readonly VertexSelectionHandler _vertexSelectionHandler;

        public UIDocument UIDoc { get; }
        public UIApplication App { get; }
        public ElementId RoofId { get; }
        private readonly Action<string> _log;

        public List<XYZ> RoofVertexPositions { get; set; }
        public Window ParentWindow { get; set; }

        /// <summary>
        /// Main constructor — receives the pre-created vertex selection event and
        /// handler so Revit's API context requirement is satisfied.
        /// </summary>
        public AutoSlopeViewModel(
            UIDocument uidoc,
            UIApplication app,
            ElementId roofId,
            List<XYZ> drainPoints,
            VertexSelectionHandler vertexSelectionHandler,
            ExternalEvent vertexSelectionEvent)
        {
            OfficeOpenXml.ExcelPackage.LicenseContext = OfficeOpenXml.LicenseContext.NonCommercial;

            UIDoc = uidoc;
            App = app;
            RoofId = roofId;
            DrainPoints = drainPoints ?? new List<XYZ>();

            // Wire the handler to this ViewModel now that both exist.
            _vertexSelectionHandler = vertexSelectionHandler;
            _vertexSelectionHandler.SetViewModel(this);
            _vertexSelectionEvent = vertexSelectionEvent;

            // Log goes to both the ViewModel LogText and (optionally) an external sink.
            _log = null;

            ExportFolderPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                "AutoSlope_Reports");

            AutoSlopeEventManager.Init();
        }

        public void SetParentWindow(Window window)
        {
            ParentWindow = window;
        }

        public void AddLog(string message)
        {
            _log?.Invoke(message);
            Application.Current.Dispatcher.BeginInvoke(new Action(() =>
            {
                LogText += $"[{DateTime.Now:HH:mm:ss}] {message}\n";
            }));
        }

        // ─────────────────────────────────────────────────────────────────────
        // FIX: StartVertexSelection — no lazy event creation.
        // Just Hide() the window and Raise() the pre-built event.
        // Also guards against Revit rejecting the event (returns non-Accepted).
        // ─────────────────────────────────────────────────────────────────────
        private void StartVertexSelection()
        {
            if (ParentWindow != null)
                ParentWindow.Hide();

            IsSelectingVertices = true;

            ExternalEventRequest result = _vertexSelectionEvent.Raise();

            // If Revit rejected the event, restore the window immediately.
            if (result != ExternalEventRequest.Accepted)
            {
                IsSelectingVertices = false;
                RestoreWindow();
                AddLog(HelpersLogColorHelper.Yellow(
                    $"⚠ Vertex selection could not start (Revit status: {result}). " +
                    "Please try again when Revit is idle."));
            }
        }

        // ─────────────────────────────────────────────────────────────────────
        // FIX: Use Dispatcher.Invoke (synchronous) so the window is guaranteed
        // visible before the external-event thread returns.
        // Added WindowState reset + Focus() for reliable WPF re-activation.
        // ─────────────────────────────────────────────────────────────────────
        public void CancelVertexSelection()
        {
            IsSelectingVertices = false;
            Application.Current.Dispatcher.Invoke(() => RestoreWindow());
        }

        public void CompleteVertexSelection(HashSet<int> selectedIndices)
        {
            SelectedVertexIndices = selectedIndices ?? new HashSet<int>();
            IsSelectingVertices = false;

            if (selectedIndices != null && selectedIndices.Count > 0)
                AddLog(HelpersLogColorHelper.Green($"✓ Selected {selectedIndices.Count} special vertices."));
            else
                AddLog(HelpersLogColorHelper.Yellow("⚠ No special vertices selected."));

            RunCommand.NotifyCanExecuteChanged();

            // Synchronous Invoke — window is shown before this thread continues.
            Application.Current.Dispatcher.Invoke(() => RestoreWindow());
        }

        /// <summary>
        /// Centralised window restore — called from both Complete and Cancel paths.
        /// Must be called on the UI thread (Dispatcher.Invoke).
        /// </summary>
        private void RestoreWindow()
        {
            if (ParentWindow == null) return;
            ParentWindow.WindowState = WindowState.Normal;
            ParentWindow.Show();
            ParentWindow.Activate();
            ParentWindow.Focus();
        }

        public void ClearDrainPoints()
        {
            DrainPoints = new List<XYZ>();
            AddLog(HelpersLogColorHelper.Yellow("⚠ Drain points cleared."));
            Raise(nameof(DrainCountDisplay));
            Raise(nameof(CanRun));
            Raise(nameof(SummaryText));
        }

        private void ClearVertexSelection()
        {
            SelectedVertexIndices.Clear();
            AddLog("Cleared all special vertex selections.");
            Raise(nameof(SpecialVertexCountDisplay));
            Raise(nameof(SummaryText));
            RunCommand.NotifyCanExecuteChanged();
        }

        private void RunAutoSlope()
        {
            if (HasRun) return;

            if (SelectedVertexIndices == null || SelectedVertexIndices.Count == 0)
            {
                AddLog(HelpersLogColorHelper.Red("Error: At least 1 special vertex must be selected."));
                return;
            }

            HasRun = true;
            LogText = "";
            AddLog("Starting AutoSlope...");

            if (ExportToExcel && !Directory.Exists(ExportFolderPath))
            {
                try { Directory.CreateDirectory(ExportFolderPath); AddLog($"Created export directory: {ExportFolderPath}"); }
                catch (Exception ex) { AddLog($"Warning: Failed to create export directory: {ex.Message}"); }
            }

            AutoSlopeHandler.Payload = new AutoSlopePayload
            {
                RoofId = RoofId,
                DrainPoints = DrainPoints,
                SlopePercent = SlopePercent,
                ThresholdMeters = ThresholdMeters,
                Vm = this,
                Log = AddLog,
                ExportConfig = new ExportConfig
                {
                    ExportPath = ExportFolderPath,
                    ExportToExcel = ExportToExcel,
                    IncludeVertexDetails = IncludeVertexDetails
                },
                EnableDrainTolerance = EnableDrainTolerance,
                DrainToleranceMm = DrainToleranceMm,
                SpecialSlopePercent = SpecialSlopePercent,
                RemainingSlopePercent = RemainingSlopePercent,
                SelectedVertexIndices = new HashSet<int>(SelectedVertexIndices),
                VertexSlopeMapping = new Dictionary<int, double>()
            };

            AutoSlopeEventManager.Event.Raise();
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
            if (!HasRun)
            {
                AddLog("Warning: Run AutoSlope first to export results.");
                return;
            }

            try
            {
                var filePath = DialogService.ShowSaveFileDialog(
                    "Excel files (*.xlsx)|*.xlsx|All files (*.*)|*.*",
                    ExportFolderPath,
                    $"AutoSlope_Results_{DateTime.Now:yyyyMMdd_HHmmss}.xlsx");

                if (!string.IsNullOrEmpty(filePath))
                {
                    using (var package = new OfficeOpenXml.ExcelPackage())
                    {
                        var sheet = package.Workbook.Worksheets.Add("AutoSlope Results");

                        sheet.Cells[1, 1].Value = "AutoSlope Results Export";
                        sheet.Cells[1, 1, 1, 2].Merge = true;
                        sheet.Cells[1, 1].Style.Font.Bold = true;
                        sheet.Cells[1, 1].Style.Font.Size = 14;

                        sheet.Cells[2, 1].Value = "Export Date:";
                        sheet.Cells[2, 1].Style.Font.Bold = true;
                        sheet.Cells[2, 2].Value = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");

                        int row = 4;
                        sheet.Cells[row, 1].Value = "Parameter";
                        sheet.Cells[row, 2].Value = "Value";
                        sheet.Cells[row, 1, row, 2].Style.Font.Bold = true;
                        sheet.Cells[row, 1, row, 2].Style.Fill.PatternType = OfficeOpenXml.Style.ExcelFillStyle.Solid;
                        sheet.Cells[row, 1, row, 2].Style.Fill.BackgroundColor.SetColor(System.Drawing.Color.LightGray);
                        row++;

                        AddSummaryRow(sheet, ref row, "Special Slope Percentage", $"{SpecialSlopePercent}%");
                        AddSummaryRow(sheet, ref row, "Remaining Slope Percentage", $"{RemainingSlopePercent}%");
                        AddSummaryRow(sheet, ref row, "Special Vertices Count", SpecialVertexCount);
                        AddSummaryRow(sheet, ref row, "Drain Points Count", DrainPoints?.Count ?? 0);
                        AddSummaryRow(sheet, ref row, "Vertices Processed", VerticesProcessed);
                        AddSummaryRow(sheet, ref row, "Vertices Skipped", VerticesSkipped);
                        AddSummaryRow(sheet, ref row, "Highest Elevation (mm)", $"{HighestElevation_mm:0}");
                        AddSummaryRow(sheet, ref row, "Longest Path (m)", $"{LongestPath_m:0.00}");
                        AddSummaryRow(sheet, ref row, "Run Duration (sec)", RunDuration_sec);
                        AddSummaryRow(sheet, ref row, "Run Date", RunDate);
                        AddSummaryRow(sheet, ref row, "Threshold (m)", ThresholdMeters);
                        AddSummaryRow(sheet, ref row, "Drain Tolerance Enabled", EnableDrainTolerance ? "Yes" : "No");
                        AddSummaryRow(sheet, ref row, "Drain Tolerance (mm)", EnableDrainTolerance ? DrainToleranceMm.ToString() : "N/A");
                        AddSummaryRow(sheet, ref row, "Export Folder", ExportFolderPath);

                        sheet.Cells[sheet.Dimension.Address].AutoFitColumns();
                        package.SaveAs(new FileInfo(filePath));
                    }
                    AddLog($"Results exported to: {filePath}");
                }
            }
            catch (Exception ex)
            {
                AddLog($"Error exporting results: {ex.Message}");
            }
        }

        private void AddSummaryRow(OfficeOpenXml.ExcelWorksheet sheet, ref int row, string label, object value)
        {
            sheet.Cells[row, 1].Value = label;
            sheet.Cells[row, 1].Style.Font.Bold = true;
            sheet.Cells[row, 2].Value = value;
            row++;
        }
    }
}