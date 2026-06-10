// =======================================================
// File: AutoSlopeViewModel.cs
// NEW CHANGES:
//   - IncludeVertexDetails (Detailed version) removed from UI
//     and hardcoded to false in ExportConfig.
//   - EnableDrainTolerance defaults to true.
//   - DrainToleranceMm defaults to AppConstants.DefaultDrainToleranceMm (500).
//   - ExportToExcel defaults to true (unchanged).
//   - ExportResults now uses ExcelExportService instead of
//     ExcelExportHelper directly (EPPlus isolation).
//   - After a successful export, MessageBox.Show asks the user
//     whether to open the file immediately.
//
// Fixes applied:
//   #1  Removed unused Action<string> log constructor parameter.
//   #5  Replaced bool HasRun with RunState enum so StatusMessage
//       correctly shows "Ready" / "Processing..." / "Completed".
//   #7  AddLog uses Dispatcher.CheckAccess() so callers already
//       on the UI thread don't incur an unnecessary async hop.
//   #9  DrainToleranceMm changed from int to match payload type
//       (both are now int – see AutoSlopePayload fix).
//   #10 Added debug logging to track FinalDrainCount updates
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

        // ── Fix #5: RunState enum replaces bare bool HasRun ──────────────────
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
                // Mirror HasRun semantics for command CanExecute
                RunCommand.NotifyCanExecuteChanged();
                ExportResultsCommand.NotifyCanExecuteChanged();
            }
        }

        // Convenience for CanExecute predicates
        private bool IsRunning => _state == RunState.Running;
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

        // Fix #9: int throughout (was double in payload, int in VM — now consistent)
        private int _drainToleranceMm = AppConstants.DefaultDrainToleranceMm;
        public int DrainToleranceMm
        {
            get => _drainToleranceMm;
            set { _drainToleranceMm = value; Raise(); }
        }

        private bool _enableDrainTolerance = true;
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

        // Detailed vertex export removed from UI — compact export only.

        // ── Log ───────────────────────────────────────────────────────────────
        private string _logText = string.Empty;
        public string LogText
        {
            get => _logText;
            set { _logText = value; Raise(); }
        }

        // ── Fix #5: Status derived from RunState, not a bool ─────────────────
        public string StatusMessage => _state switch
        {
            RunState.Running => "Processing...",
            RunState.Done => "Completed",
            _ => "Ready to run"
        };

        public string StatusColor => _state switch
        {
            RunState.Running => AppConstants.Color_Processing,
            RunState.Done => AppConstants.Color_Success,
            _ => AppConstants.Color_Ready
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
        public int PickedDrainCount
        {
            get => _pickedDrainCount;
            set
            {
                _pickedDrainCount = value;
                Raise();
                Raise(nameof(SummaryText));
                AddLog($"DEBUG: PickedDrainCount set to {value}");
            }
        }

        private int _finalDrainCount;
        /// <summary>Final drain count after tolerance is applied — used in calculation.</summary>
        public int FinalDrainCount
        {
            get => _finalDrainCount;
            set
            {
                _finalDrainCount = value;
                Raise();
                Raise(nameof(SummaryText));
                AddLog($"DEBUG: FinalDrainCount set to {value}");
            }
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

        // Cached result — used by ExportResultsCommand
        private AutoSlopeResult _lastResult;

        public string SummaryText =>
$@"Applied Slope Percentage : {AppliedSlopeDisplay}
Vertices Processed       : {VerticesProcessed}
Vertices Skipped         : {VerticesSkipped}
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
        public RelayCommand ClearLogCommand => _clearLogCommand ??= new RelayCommand(
            ClearLog);

        private RelayCommand _exportResultsCommand;
        public RelayCommand ExportResultsCommand => _exportResultsCommand ??= new RelayCommand(
            ExportResults, () => IsComplete && _lastResult?.Success == true);

        // ── Constructor fields ────────────────────────────────────────────────
        public UIDocument UIDoc { get; }
        public UIApplication App { get; }
        public ElementId RoofId { get; }
        public List<XYZ> DrainPoints { get; }

        // Fix #1: removed unused Action<string> log parameter
        public AutoSlopeViewModel(
            UIDocument uidoc,
            UIApplication app,
            ElementId roofId,
            List<XYZ> drainPoints)
        {
            UIDoc = uidoc;
            App = app;
            RoofId = roofId;
            DrainPoints = drainPoints;

            // Set both counts immediately from raw user selection.
            // FinalDrainCount will be updated by OnCompleted after the engine
            // runs — if tolerance is enabled it may grow; otherwise it stays equal.
            PickedDrainCount = drainPoints?.Count ?? 0;
            FinalDrainCount = drainPoints?.Count ?? 0;

            AddLog($"DEBUG: Constructor - PickedDrainCount={PickedDrainCount}, FinalDrainCount={FinalDrainCount}");

            ExportFolderPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                AppConstants.DefaultExportFolder);

            AutoSlopeEventManager.Init();
        }

        // ── RunAutoSlope ──────────────────────────────────────────────────────
        private void RunAutoSlope()
        {
            if (IsRunning || IsComplete) return;

            State = RunState.Running;   // Fix #5: set Running state
            LogText = string.Empty;
            AddLog("Starting AutoSlope...");

            // Ensure export directory exists
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
                PickedDrainPoints = DrainPoints,   // raw user selection
                DrainPoints = DrainPoints,   // engine will expand this with tolerance
                SlopePercent = SlopePercent,
                ThresholdMeters = ThresholdMeters,
                EnableDrainTolerance = EnableDrainTolerance,
                DrainToleranceMm = DrainToleranceMm,   // Fix #9: both int now
                ProjectTitle = UIDoc?.Document?.Title ?? "Unknown Project",
                Log = AddLog,
                ExportConfig = new ExportConfig
                {
                    ExportPath = ExportFolderPath,
                    ExportToExcel = ExportToExcel,
                    IncludeVertexDetails = false   // detailed export removed from UI
                },

                OnCompleted = result =>
                {
                    Application.Current.Dispatcher.BeginInvoke(new Action(() =>
                    {
                        AddLog($"DEBUG: OnCompleted callback received - Success={result.Success}, PickedCount={result.PickedDrainCount}, FinalCount={result.FinalDrainCount}");

                        if (!result.Success)
                        {
                            AddLog($"AutoSlope failed: {result.ErrorMessage}");
                            // Reset to Ready so the user can correct settings and retry
                            State = RunState.Ready;
                            return;
                        }

                        _lastResult = result;
                        VerticesProcessed = result.VerticesProcessed;
                        VerticesSkipped = result.VerticesSkipped;

                        AddLog($"DEBUG: About to set PickedDrainCount to {result.PickedDrainCount}");
                        PickedDrainCount = result.PickedDrainCount;

                        AddLog($"DEBUG: About to set FinalDrainCount to {result.FinalDrainCount}");
                        FinalDrainCount = result.FinalDrainCount;

                        AddLog($"DEBUG: After setting - PickedDrainCount={PickedDrainCount}, FinalDrainCount={FinalDrainCount}");

                        HighestElevation_mm = result.HighestElevation_mm;
                        LongestPath_m = result.LongestPath_m;
                        RunDuration_sec = result.RunDuration_sec;
                        RunDate = result.RunDate;

                        State = RunState.Done;   // Fix #5: show "Completed"
                        ExportResultsCommand.NotifyCanExecuteChanged();

                        AddLog($"DEBUG: State set to Done, UI should now show FinalDrainCount={FinalDrainCount}");
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

            string filePath = DialogService.ShowSaveFileDialog(
                "Excel files (*.xlsx)|*.xlsx|All files (*.*)|*.*",
                ExportFolderPath,
                $"AutoSlope_Results_{DateTime.Now:yyyyMMdd_HHmmss}.xlsx");

            if (string.IsNullOrEmpty(filePath)) return;

            string savedPath = ExcelExportService.ExportResultsSummary(
                filePath,
                _lastResult,
                SlopePercent,
                ThresholdMeters,
                EnableDrainTolerance,
                DrainToleranceMm,
                ExportFolderPath,
                AddLog);

            if (!string.IsNullOrEmpty(savedPath))
            {
                AddLog($"✅ Results exported to: {savedPath}");

                var answer = System.Windows.MessageBox.Show(
                    "Excel file saved. Open it now?",
                    "Export Complete",
                    System.Windows.MessageBoxButton.YesNo,
                    System.Windows.MessageBoxImage.Question);

                if (answer == System.Windows.MessageBoxResult.Yes)
                    System.Diagnostics.Process.Start(savedPath);
            }
        }

        // ── AddLog ────────────────────────────────────────────────────────────
        // Fix #7: use CheckAccess() to avoid unnecessary async dispatch when
        // the caller is already on the UI thread (e.g. ClearLog, BrowseForFolder).
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