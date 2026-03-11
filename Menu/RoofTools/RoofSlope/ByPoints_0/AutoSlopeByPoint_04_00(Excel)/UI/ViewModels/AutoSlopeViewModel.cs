using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using CommunityToolkit.Mvvm.Input;
using OfficeOpenXml;
using OfficeOpenXml.Style;
using Revit26_Plugin.AutoSlopeByPoint_04.Core.Models;
using Revit26_Plugin.AutoSlopeByPoint_04.Infrastructure.ExternalEvents;
using Revit26_Plugin.AutoSlopeByPoint_04.Infrastructure.Helpers;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.IO;
using System.Runtime.CompilerServices;
using System.Windows;
using OfficeOpenXml;
using OfficeOpenXml.Style;
using System.Drawing;

namespace Revit26_Plugin.AutoSlopeByPoint_04.UI.ViewModels
{
    public class AutoSlopeViewModel : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;
        private void Raise([CallerMemberName] string name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

        public List<double> SlopeOptions { get; } = new List<double> { 0.5, 1.0, 1.5, 2.0, 2.5 };

        private double _slopePercent = 1.5;
        public double SlopePercent
        {
            get => _slopePercent;
            set { _slopePercent = value; Raise(); }
        }

        private int _thresholdMeters = 50;
        public int ThresholdMeters
        {
            get => _thresholdMeters;
            set { _thresholdMeters = value; Raise(); }
        }

        private string _exportFolderPath;
        public string ExportFolderPath
        {
            get => _exportFolderPath;
            set { _exportFolderPath = value; Raise(); }
        }

        private bool _exportToExcel = true;
        public bool ExportToExcel
        {
            get => _exportToExcel;
            set { _exportToExcel = value; Raise(); }
        }

        private bool _includeVertexDetails = true;
        public bool IncludeVertexDetails
        {
            get => _includeVertexDetails;
            set { _includeVertexDetails = value; Raise(); }
        }

        private string _logText = "";
        public string LogText
        {
            get => _logText;
            set { _logText = value; Raise(); }
        }

        public string StatusMessage => HasRun ? "Processing..." : "Ready to run";
        public string StatusColor => HasRun ? "#E67E22" : "#27AE60";

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

        private RelayCommand _runCommand;
        public RelayCommand RunCommand => _runCommand ??= new RelayCommand(
            RunAutoSlope, () => !HasRun);

        private RelayCommand _browseFolderCommand;
        public RelayCommand BrowseFolderCommand => _browseFolderCommand ??= new RelayCommand(
            BrowseForFolder);

        private RelayCommand _clearLogCommand;
        public RelayCommand ClearLogCommand => _clearLogCommand ??= new RelayCommand(
            ClearLog);

        private RelayCommand _exportResultsCommand;
        public RelayCommand ExportResultsCommand => _exportResultsCommand ??= new RelayCommand(
            ExportResults, () => HasRun);

        public UIDocument UIDoc { get; }
        public UIApplication App { get; }
        public ElementId RoofId { get; }
        public List<XYZ> DrainPoints { get; }
        private readonly Action<string> _log;

        public AutoSlopeViewModel(
            UIDocument uidoc,
            UIApplication app,
            ElementId roofId,
            List<XYZ> drainPoints,
            Action<string> log)
        {
            // Set EPPlus license context - FULLY QUALIFIED to avoid ambiguity
            OfficeOpenXml.ExcelPackage.LicenseContext = OfficeOpenXml.LicenseContext.NonCommercial;

            UIDoc = uidoc;
            App = app;
            RoofId = roofId;
            DrainPoints = drainPoints;
            _log = log;

            ExportFolderPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                "AutoSlope_Reports");

            AutoSlopeEventManager.Init();
        }

        private void RunAutoSlope()
        {
            if (HasRun) return;
            HasRun = true;
            LogText = "";
            AddLog("Starting AutoSlope...");

            if (ExportToExcel && !Directory.Exists(ExportFolderPath))
            {
                try
                {
                    Directory.CreateDirectory(ExportFolderPath);
                    AddLog($"Created export directory: {ExportFolderPath}");
                }
                catch (Exception ex)
                {
                    AddLog($"Warning: Failed to create export directory: {ex.Message}");
                }
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
                }
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
                    // Use fully qualified namespace
                    using (var package = new OfficeOpenXml.ExcelPackage())
                    {
                        var sheet = package.Workbook.Worksheets.Add("AutoSlope Results");

                        // Title
                        sheet.Cells[1, 1].Value = "AutoSlope Results Export";
                        sheet.Cells[1, 1, 1, 2].Merge = true;
                        sheet.Cells[1, 1].Style.Font.Bold = true;
                        sheet.Cells[1, 1].Style.Font.Size = 14;

                        // Export Date
                        sheet.Cells[2, 1].Value = "Export Date:";
                        sheet.Cells[2, 1].Style.Font.Bold = true;
                        sheet.Cells[2, 2].Value = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");

                        // Headers
                        int row = 4;
                        sheet.Cells[row, 1].Value = "Parameter";
                        sheet.Cells[row, 2].Value = "Value";
                        sheet.Cells[row, 1, row, 2].Style.Font.Bold = true;
                        sheet.Cells[row, 1, row, 2].Style.Fill.PatternType = OfficeOpenXml.Style.ExcelFillStyle.Solid;
                        sheet.Cells[row, 1, row, 2].Style.Fill.BackgroundColor.SetColor(System.Drawing.Color.LightGray);
                        row++;

                        // Add summary data
                        AddSummaryRow(sheet, ref row, "Vertices Processed", VerticesProcessed);
                        AddSummaryRow(sheet, ref row, "Vertices Skipped", VerticesSkipped);
                        AddSummaryRow(sheet, ref row, "Drain Count", DrainCount);
                        AddSummaryRow(sheet, ref row, "Highest Elevation (mm)", $"{HighestElevation_mm:0}");
                        AddSummaryRow(sheet, ref row, "Longest Path (m)", $"{LongestPath_m:0.00}");
                        AddSummaryRow(sheet, ref row, "Run Duration (sec)", RunDuration_sec);
                        AddSummaryRow(sheet, ref row, "Run Date", RunDate);
                        AddSummaryRow(sheet, ref row, "Slope Percentage", $"{SlopePercent}%");
                        AddSummaryRow(sheet, ref row, "Threshold (m)", ThresholdMeters);
                        AddSummaryRow(sheet, ref row, "Export Folder", ExportFolderPath);

                        // Auto-fit columns
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

        private void AddLog(string message)
        {
            _log?.Invoke(message);
            Application.Current.Dispatcher.BeginInvoke(new Action(() =>
            {
                LogText += $"[{DateTime.Now:HH:mm:ss}] {message}\n";
            }));
        }
    }
}