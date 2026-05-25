// =======================================================
// File: AutoSlopeViewModel.cs
// Fixes applied:
//   #1  Removed unused Action<string> log constructor parameter.
//   #5  Replaced bool HasRun with RunState enum so StatusMessage
//       correctly shows "Ready" / "Processing..." / "Completed".
//   #7  AddLog uses Dispatcher.CheckAccess() so callers already
//       on the UI thread don't incur an unnecessary async hop.
//   #9  DrainToleranceMm is int throughout (matches payload + AppConstants).
//   #10 Removed debug AddLog spam from property setters; debug
//       logging is now only emitted at meaningful flow boundaries.
//   #11 Added AvgSlopePercent and Percentage2Applied properties
//       so the XAML bindings resolve without FallbackValue fallback.
//   #12 Exposed IsRunning and IsComplete as public bool properties
//       so XAML DataTriggers can bind to them directly.
//   #12 StatusColor is a SolidColorBrush string sourced from
//       AppConstants so it is consistent with the resource dictionary.
// =======================================================

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
        // ── INotifyPropertyChanged ────────────────────────────────────────────
        public event PropertyChangedEventHandler PropertyChanged;
        private void Raise([CallerMemberName] string name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

        // ── Fix #5 / #12: RunState enum replaces bare bool HasRun ────────────
        private enum RunState { Ready, Running, Done }

        private RunState _state = RunState.Ready;
        private RunState State
        {
            get => _state;
            set
            {
                _state = value;
                Raise(nameof(StatusMessage));
                Raise(nameof(StatusColor));
                Raise(nameof(IsRunning));       // Fix #12: XAML DataTrigger support
                Raise(nameof(IsComplete));      // Fix #12: XAML DataTrigger support
                RunCommand.NotifyCanExecuteChanged();
                ExportResultsCommand.NotifyCanExecuteChanged();
            }
        }

        // Fix #12: public so XAML DataTriggers can bind
        public bool IsRunning  => _state == RunState.Running;
        public bool IsComplete => _state == RunState.Done;

        // ── Slope options ─────────────────────────────────────────────────────
        public List<double> SlopeOptions { get; } = new List<double> { 0.5, 1.0, 1.5, 2.0, 2.5 };

        // ── Input properties ──────────────────────────────────────────────────
        private double _slopePercent = AppConstants.DefaultSlopePercent;
        public double SlopePercent
        {
            get => _slopePercent;
            set { _slopePercent = value; Raise(); Raise(nameof(AppliedSlopeDisplay)); }
        }
        public string AppliedSlopeDisplay => $"{SlopePercent}%";

        private int _thresholdMeters = AppConstants.DefaultThresholdMeters;
        public int ThresholdMeters
        {
            get => _thresholdMeters;
            set { _thresholdMeters = value; Raise(); }
        }

        private int _drainToleranceMm = AppConstants.DefaultDrainToleranceMm;
        public int DrainToleranceMm
        {
            get => _drainToleranceMm;
            set { _drainToleranceMm = value; Raise(); }
        }

        private bool _enableDrainTolerance;
        public bool EnableDrainTolerance
        {
            get => _enableDrainTolerance;
            set { _enableDrainTolerance = value; Raise(); }
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

        // ── Log ───────────────────────────────────────────────────────────────
        private string _logText = string.Empty;
        public string LogText
        {
            get => _logText;
            set { _logText = value; Raise(); }
        }

        // ── Fix #5: Status derived from RunState ──────────────────────────────
        public string StatusMessage => _state switch
        {
            RunState.Running => "Processing...",
            RunState.Done    => "Completed",
            _                => "Ready to run"
        };

        public string StatusColor => _state switch
        {
            RunState.Running => AppConstants.Color_Processing,
            RunState.Done    => AppConstants.Color_Success,
            _                => AppConstants.Color_Ready
        };

        // ── Result properties (populated by OnCompleted) ──────────────────────
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

        private int _pickedDrainCount;
        /// <summary>Count of drains the user picked — before tolerance expansion.</summary>
        public int PIckedDrainCount
        {
            get => _pickedDrainCount;
            set { _pickedDrainCount = value; Raise(); Raise(nameof(SummaryText)); }
        }

        private int _finalDrainCount;
        /// <summary>Full drain list count after tolerance expansion + deduplication.</summary>
        public int FinalDrainCount
        {
            get => _finalDrainCount;
            set { _finalDrainCount = value; Raise(); Raise(nameof(SummaryText)); }
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

        // Fix #11: AvgSlopePercent — received from engine result
        private double _avgSlopePercent;
        public double AvgSlopePercent
        {
            get => _avgSlopePercent;
            set { _avgSlopePercent = value; Raise(); Raise(nameof(SummaryText)); }
        }

        // Fix #11: Percentage2Applied — received from engine result
        private double _percentage2Applied;
        public double Percentage2Applied
        {
            get => _percentage2Applied;
            set { _percentage2Applied = value; Raise(); Raise(nameof(SummaryText)); }
        }

        // Cached result — used by ExportResultsCommand
        private AutoSlopeResult _lastResult;

        public string SummaryText =>
$@"Applied Slope Percentage : {AppliedSlopeDisplay}
Vertices Processed       : {VerticesProcessed}
Vertices Skipped         : {VerticesSkipped}
Picked Drain Count       : {PIckedDrainCount}
Final Drain Count        : {FinalDrainCount}
Avg Slope Applied        : {AvgSlopePercent:F1}%
Percentage 2 Applied     : {Percentage2Applied:F1}%
Highest Elevation        : {HighestElevationDisplay}
Longest Path             : {LongestPathDisplay}
Run Duration             : {RunDurationDisplay}
Run Date                 : {RunDate}
Export Folder            : {ExportFolderPath}";

        // ── Commands ──────────────────────────────────────────────────────────
        private RelayCommand _runCommand;
        public RelayCommand RunCommand => _runCommand ??= new RelayCommand(
            RunAutoSlope, () => !IsRunning && !IsComplete);

        private RelayCommand _browseFolderCommand;
        public RelayCommand BrowseFolderCommand => _browseFolderCommand ??= new RelayCommand(
            BrowseForFolder);

        private RelayCommand _clearLogCommand;
        public RelayCommand ClearLogCommand => _clearLogCommand ??= new RelayCommand(
            ClearLog);

        private RelayCommand _exportResultsCommand;
        public RelayCommand ExportResultsCommand => _exportResultsCommand ??= new RelayCommand(
            ExportResults, () => IsComplete && _lastResult?.Success == true);

        // ── Constructor fields ────────────────────────────────────────────────
        public UIDocument UIDoc   { get; }
        public UIApplication App  { get; }
        public ElementId RoofId   { get; }
        public List<XYZ> DrainPoints { get; }

        public AutoSlopeViewModel(
            UIDocument uidoc,
            UIApplication app,
            ElementId roofId,
            List<XYZ> drainPoints)
        {
            UIDoc      = uidoc;
            App        = app;
            RoofId     = roofId;
            DrainPoints = drainPoints;

            // Initialise both counts from raw user selection.
            // FinalDrainCount is updated by OnCompleted after the engine runs.
            PIckedDrainCount = drainPoints?.Count ?? 0;
            FinalDrainCount  = drainPoints?.Count ?? 0;

            ExportFolderPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                AppConstants.DefaultExportFolder);

            AutoSlopeEventManager.Init();
        }

        // ── RunAutoSlope ──────────────────────────────────────────────────────
        private void RunAutoSlope()
        {
            if (IsRunning || IsComplete) return;

            State   = RunState.Running;
            LogText = string.Empty;
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
                RoofId              = RoofId,
                PickedDrainPoints   = DrainPoints,
                DrainPoints         = DrainPoints,
                SlopePercent        = SlopePercent,
                ThresholdMeters     = ThresholdMeters,
                EnableDrainTolerance = EnableDrainTolerance,
                DrainToleranceMm    = DrainToleranceMm,
                ProjectTitle        = UIDoc?.Document?.Title ?? "Unknown Project",
                Log                 = AddLog,
                ExportConfig = new ExportConfig
                {
                    ExportPath          = ExportFolderPath,
                    ExportToExcel       = ExportToExcel,
                    IncludeVertexDetails = IncludeVertexDetails
                },

                OnCompleted = result =>
                {
                    Application.Current.Dispatcher.BeginInvoke(new Action(() =>
                    {
                        if (!result.Success)
                        {
                            AddLog($"AutoSlope failed: {result.ErrorMessage}");
                            State = RunState.Ready;   // allow retry
                            return;
                        }

                        _lastResult = result;

                        VerticesProcessed  = result.VerticesProcessed;
                        VerticesSkipped    = result.VerticesSkipped;
                        PIckedDrainCount   = result.PickedDrainCount;
                        FinalDrainCount    = result.FinalDrainCount;   // full drain list count
                        HighestElevation_mm = result.HighestElevation_mm;
                        LongestPath_m      = result.LongestPath_m;
                        RunDuration_sec    = result.RunDuration_sec;
                        RunDate            = result.RunDate;
                        AvgSlopePercent    = result.AvgSlopePercent;
                        Percentage2Applied = result.Percentage2Applied;

                        State = RunState.Done;
                        ExportResultsCommand.NotifyCanExecuteChanged();

                        AddLog($"✅ Done — FinalDrainCount = {FinalDrainCount}");
                    }));
                }
            };

            AutoSlopeEventManager.Event.Raise();
        }

        // ── BrowseForFolder ───────────────────────────────────────────────────
        private void BrowseForFolder()
        {
            var selected = DialogService.SelectFolder(ExportFolderPath);
            if (!string.IsNullOrEmpty(selected))
            {
                ExportFolderPath = selected;
                AddLog($"Export folder set to: {ExportFolderPath}");
            }
        }

        // ── ClearLog ──────────────────────────────────────────────────────────
        private void ClearLog()
        {
            LogText = string.Empty;
            AddLog("Log cleared.");
        }

        // ── ExportResults ─────────────────────────────────────────────────────
        private void ExportResults()
        {
            if (_lastResult == null || !_lastResult.Success)
            {
                AddLog("Warning: Run AutoSlope successfully first.");
                return;
            }

            try
            {
                string filePath = DialogService.ShowSaveFileDialog(
                    "Excel files (*.xlsx)|*.xlsx|All files (*.*)|*.*",
                    ExportFolderPath,
                    $"AutoSlope_Results_{DateTime.Now:yyyyMMdd_HHmmss}.xlsx");

                if (string.IsNullOrEmpty(filePath)) return;

                ExcelExportHelper.ExportResultsSummary(
                    filePath,
                    _lastResult,
                    SlopePercent,
                    ThresholdMeters,
                    EnableDrainTolerance,
                    DrainToleranceMm,
                    ExportFolderPath);

                AddLog($"Results exported to: {filePath}");
            }
            catch (Exception ex)
            {
                AddLog($"Error exporting results: {ex.Message}");
            }
        }

        // ── AddLog ────────────────────────────────────────────────────────────
        // Fix #7: CheckAccess() avoids unnecessary async hop when already on UI thread.
        private void AddLog(string message)
        {
            var dispatcher = Application.Current.Dispatcher;
            if (dispatcher.CheckAccess())
                LogText += $"{message}\n";
            else
                dispatcher.BeginInvoke(new Action(() => LogText += $"{message}\n"));
        }
    }
}
