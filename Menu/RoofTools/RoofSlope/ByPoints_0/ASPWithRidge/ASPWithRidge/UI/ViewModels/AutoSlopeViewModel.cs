// =======================================================
// File: AutoSlopeViewModel.cs
// Namespace: Revit26_Plugin.AutoSlopeByPoint.WithRidge
// Changes vs original:
//   + RidgeDetectionEnabled   — bool toggle, bound to UI checkbox.
//   + DrainGroupRadiusMm      — int, bound to UI TextBox/Slider.
//     Default = AppConstants.DefaultDrainGroupRadiusMm (500).
//   + RidgeLineToleranceMm   — int, bound to UI TextBox/Slider.
//     Default = AppConstants.DefaultRidgeLineToleranceMm (500).
//   RidgeRatioTolerance removed — replaced by drain-group geometry.
//   + RidgePointsDetected     — result property, shown in summary.
//   + SummaryText             — includes ridge count line.
//   + RunAutoSlope            — passes ridge settings to payload.
//   + ExportResults           — passes ridge settings to Excel export.
// =======================================================

using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using CommunityToolkit.Mvvm.Input;
using Revit26_Plugin.AutoSlopeByPoint.WithRidge.Core.Models;
using Revit26_Plugin.AutoSlopeByPoint.WithRidge.Infrastructure.ExternalEvents;
using Revit26_Plugin.AutoSlopeByPoint.WithRidge.Infrastructure.Helpers;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using System.Windows;

namespace Revit26_Plugin.AutoSlopeByPoint.WithRidge.UI.ViewModels
{
    public class AutoSlopeViewModel : INotifyPropertyChanged
    {
        // ── INotifyPropertyChanged ────────────────────────────────────────────
        public event PropertyChangedEventHandler PropertyChanged;
        private void Raise([CallerMemberName] string name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

        // ── RunState ──────────────────────────────────────────────────────────
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
                RunCommand.NotifyCanExecuteChanged();
                ExportResultsCommand.NotifyCanExecuteChanged();
            }
        }

        private bool IsRunning  => _state == RunState.Running;
        private bool IsComplete => _state == RunState.Done;

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

        // ── Ridge settings (user-adjustable per spec Q2 / Q5) ────────────────

        private bool _ridgeDetectionEnabled = AppConstants.DefaultRidgeDetectionEnabled;
        /// <summary>
        /// Master toggle for ridge detection.
        /// Bind to a CheckBox in the UI.
        /// When false the engine behaves exactly like the original version.
        /// </summary>
        public bool RidgeDetectionEnabled
        {
            get => _ridgeDetectionEnabled;
            set { _ridgeDetectionEnabled = value; Raise(); Raise(nameof(RidgeSettingsVisible)); }
        }

        /// <summary>Controls visibility of ridge sub-settings in the UI.</summary>
        public bool RidgeSettingsVisible => RidgeDetectionEnabled;

        private int _drainGroupRadiusMm = AppConstants.DefaultDrainGroupRadiusMm;
        /// <summary>
        /// Drain grouping radius in mm.
        /// Two drains within this XY distance belong to the same group.
        /// Bind to a TextBox or IntegerUpDown in the UI.
        /// </summary>
        public int DrainGroupRadiusMm
        {
            get => _drainGroupRadiusMm;
            set { _drainGroupRadiusMm = Math.Max(1, value); Raise(); }
        }

        private int _ridgeLineToleranceMm = AppConstants.DefaultRidgeLineToleranceMm;
        /// <summary>
        /// Ridge line membership tolerance in mm.
        /// A vertex within this XY perpendicular distance of the ridge line
        /// is treated as a ridge point.
        /// Bind to a TextBox or IntegerUpDown in the UI.
        /// </summary>
        public int RidgeLineToleranceMm
        {
            get => _ridgeLineToleranceMm;
            set { _ridgeLineToleranceMm = Math.Max(1, value); Raise(); }
        }

        // ── Export settings ───────────────────────────────────────────────────
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

        // ── Status ────────────────────────────────────────────────────────────
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

        // ── Result properties ─────────────────────────────────────────────────
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
        public int PickedDrainCount
        {
            get => _pickedDrainCount;
            set { _pickedDrainCount = value; Raise(); Raise(nameof(SummaryText)); }
        }

        private int _finalDrainCount;
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

        private int _ridgePointsDetected;
        public int RidgePointsDetected
        {
            get => _ridgePointsDetected;
            set { _ridgePointsDetected = value; Raise(); Raise(nameof(SummaryText)); }
        }

        private AutoSlopeResult _lastResult;

        public string SummaryText =>
$@"Applied Slope Percentage : {AppliedSlopeDisplay}
Vertices Processed       : {VerticesProcessed}
Vertices Skipped         : {VerticesSkipped}
Ridge Points Detected    : {RidgePointsDetected}
Picked Drain Count       : {PickedDrainCount}
Final Drain Count        : {FinalDrainCount}
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
        public RelayCommand ClearLogCommand => _clearLogCommand ??= new RelayCommand(ClearLog);

        private RelayCommand _exportResultsCommand;
        public RelayCommand ExportResultsCommand => _exportResultsCommand ??= new RelayCommand(
            ExportResults, () => IsComplete && _lastResult?.Success == true);

        // ── Constructor ───────────────────────────────────────────────────────
        public UIDocument UIDoc { get; }
        public UIApplication App { get; }
        public ElementId RoofId { get; }
        public List<XYZ> DrainPoints { get; }

        public AutoSlopeViewModel(
            UIDocument uidoc,
            UIApplication app,
            ElementId roofId,
            List<XYZ> drainPoints)
        {
            UIDoc       = uidoc;
            App         = app;
            RoofId      = roofId;
            DrainPoints = drainPoints;

            PickedDrainCount = drainPoints?.Count ?? 0;
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
                RoofId                  = RoofId,
                PickedDrainPoints       = DrainPoints,
                DrainPoints             = DrainPoints,
                SlopePercent            = SlopePercent,
                ThresholdMeters         = ThresholdMeters,
                EnableDrainTolerance    = EnableDrainTolerance,
                DrainToleranceMm        = DrainToleranceMm,
                RidgeDetectionEnabled   = RidgeDetectionEnabled,
                DrainGroupRadiusMm      = DrainGroupRadiusMm,
                RidgeLineToleranceMm    = RidgeLineToleranceMm,
                ProjectTitle            = UIDoc?.Document?.Title ?? "Unknown Project",
                Log                     = AddLog,
                ExportConfig = new ExportConfig
                {
                    ExportPath           = ExportFolderPath,
                    ExportToExcel        = ExportToExcel,
                    IncludeVertexDetails = IncludeVertexDetails
                },

                OnCompleted = result =>
                {
                    Application.Current.Dispatcher.BeginInvoke(new Action(() =>
                    {
                        if (!result.Success)
                        {
                            AddLog($"AutoSlope failed: {result.ErrorMessage}");
                            State = RunState.Ready;
                            return;
                        }

                        _lastResult              = result;
                        VerticesProcessed        = result.VerticesProcessed;
                        VerticesSkipped          = result.VerticesSkipped;
                        PickedDrainCount         = result.PickedDrainCount;
                        FinalDrainCount          = result.FinalDrainCount;
                        HighestElevation_mm      = result.HighestElevation_mm;
                        LongestPath_m            = result.LongestPath_m;
                        RunDuration_sec          = result.RunDuration_sec;
                        RunDate                  = result.RunDate;
                        RidgePointsDetected      = result.RidgePointsDetected;

                        State = RunState.Done;
                        ExportResultsCommand.NotifyCanExecuteChanged();
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
                    RidgeDetectionEnabled,
                    DrainGroupRadiusMm,
                    RidgeLineToleranceMm,
                    ExportFolderPath);

                AddLog($"Results exported to: {filePath}");
            }
            catch (Exception ex)
            {
                AddLog($"Error exporting results: {ex.Message}");
            }
        }

        // ── AddLog ────────────────────────────────────────────────────────────
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
