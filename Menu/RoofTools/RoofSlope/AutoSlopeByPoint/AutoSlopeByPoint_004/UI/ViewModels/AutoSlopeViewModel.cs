using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using CommunityToolkit.Mvvm.Input;
using Revit26_Plugin.AutoSlopeByPoint_04.Core.Models;
using Revit26_Plugin.AutoSlopeByPoint_04.Infrastructure.ExternalEvents;
using Revit26_Plugin.AutoSlopeByPoint_04.Infrastructure.Helpers;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using System.Windows;

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

            if (ExportToCsv && !Directory.Exists(ExportFolderPath))
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
                    ExportToCsv = ExportToCsv,
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
                    "CSV files (*.csv)|*.csv|All files (*.*)|*.*",
                    ExportFolderPath,
                    $"AutoSlope_Results_{DateTime.Now:yyyyMMdd_HHmmss}.csv");

                if (!string.IsNullOrEmpty(filePath))
                {
                    var exportData = new List<string>
                    {
                        "AutoSlope Results Export",
                        $"Export Date: {DateTime.Now:yyyy-MM-dd HH:mm:ss}",
                        "",
                        "SUMMARY",
                        $"Vertices Processed,{VerticesProcessed}",
                        $"Vertices Skipped,{VerticesSkipped}",
                        $"Drain Count,{DrainCount}",
                        $"Highest Elevation (mm),{HighestElevation_mm:0}",
                        $"Longest Path (m),{LongestPath_m:0.00}",
                        $"Run Duration (sec),{RunDuration_sec}",
                        $"Run Date,{RunDate}",
                        $"Slope Percentage,{SlopePercent}",
                        $"Threshold (m),{ThresholdMeters}"
                    };

                    File.WriteAllLines(filePath, exportData);
                    AddLog($"Results exported to: {filePath}");
                }
            }
            catch (Exception ex)
            {
                AddLog($"Error exporting results: {ex.Message}");
            }
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