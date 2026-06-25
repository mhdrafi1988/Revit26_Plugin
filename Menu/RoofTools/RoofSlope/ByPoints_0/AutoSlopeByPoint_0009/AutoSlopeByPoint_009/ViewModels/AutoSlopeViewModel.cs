// =======================================================
// File: AutoSlopeViewModel.cs
// Namespace: Revit26_Plugin.AutoSlopeByPoint.V009
// Changes vs V06:
//   - LogText (string) replaced by LogEntries (ObservableCollection<LogEntry>)
//     from Revit26_Plugin.Shared.Models.
//   - AddLog(string) replaced by AddLog(LogEntry) — engine callback
//     now feeds structured entries directly.
//   - ExportResultsSummary log parameter updated to Action<LogEntry>.
//   - InverseBoolConverter → Revit26_Plugin.Shared.Models (no local copy).
//   - LogColorHelper removed — colour comes from LogLevelToColorConverter in XAML.
//   - ClearLog clears the ObservableCollection.
// =======================================================

using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using CommunityToolkit.Mvvm.Input;
using Revit26_Plugin.AutoSlopeByPoint.V009.Core.Models;
using Revit26_Plugin.AutoSlopeByPoint.V009.Infrastructure.ExternalEvents;
using Revit26_Plugin.AutoSlopeByPoint.V009.Infrastructure.Helpers;
using Revit26_Plugin.Shared.Models;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using System.Windows;

namespace Revit26_Plugin.AutoSlopeByPoint.V009.UI.ViewModels
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

        // ── Log (Shared LogEntry collection) ──────────────────────────────────
        /// <summary>
        /// Bound to the log ListView/ItemsControl in the View.
        /// Each entry carries LogLevel for colour-coding via LogLevelToColorConverter.
        /// </summary>
        public ObservableCollection<LogEntry> LogEntries { get; } = new ObservableCollection<LogEntry>();

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
            set
            {
                _pickedDrainCount = value;
                Raise();
                Raise(nameof(SummaryText));
                AddLog(new LogEntry(LogLevel.Info, $"DEBUG: PickedDrainCount set to {value}"));
            }
        }

        private int _finalDrainCount;
        public int FinalDrainCount
        {
            get => _finalDrainCount;
            set
            {
                _finalDrainCount = value;
                Raise();
                Raise(nameof(SummaryText));
                AddLog(new LogEntry(LogLevel.Info, $"DEBUG: FinalDrainCount set to {value}"));
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

            PickedDrainCount = drainPoints?.Count ?? 0;
            FinalDrainCount  = drainPoints?.Count ?? 0;

            AddLog(new LogEntry(LogLevel.Info,
                $"DEBUG: Constructor — PickedDrainCount={PickedDrainCount}, FinalDrainCount={FinalDrainCount}"));

            ExportFolderPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                AppConstants.DefaultExportFolder);

            AutoSlopeEventManager.Init();
        }

        // ── RunAutoSlope ──────────────────────────────────────────────────────
        private void RunAutoSlope()
        {
            if (IsRunning || IsComplete) return;

            State = RunState.Running;
            LogEntries.Clear();
            AddLog(new LogEntry(LogLevel.Info, "Starting AutoSlope..."));

            if (ExportToExcel && !Directory.Exists(ExportFolderPath))
            {
                try
                {
                    Directory.CreateDirectory(ExportFolderPath);
                    AddLog(new LogEntry(LogLevel.Info,
                        $"Created export directory: {ExportFolderPath}"));
                }
                catch (Exception ex)
                {
                    AddLog(new LogEntry(LogLevel.Warning,
                        $"Warning: Failed to create export directory: {ex.Message}"));
                }
            }

            AutoSlopeHandler.Payload = new AutoSlopePayload
            {
                RoofId             = RoofId,
                PickedDrainPoints  = DrainPoints,
                DrainPoints        = DrainPoints,
                SlopePercent       = SlopePercent,
                ThresholdMeters    = ThresholdMeters,
                EnableDrainTolerance = EnableDrainTolerance,
                DrainToleranceMm   = DrainToleranceMm,
                ProjectTitle       = UIDoc?.Document?.Title ?? "Unknown Project",
                Log                = AddLog,      // Action<LogEntry>
                ExportConfig       = new ExportConfig
                {
                    ExportPath           = ExportFolderPath,
                    ExportToExcel        = ExportToExcel,
                    IncludeVertexDetails = false
                },

                OnCompleted = result =>
                {
                    Application.Current.Dispatcher.BeginInvoke(new Action(() =>
                    {
                        AddLog(new LogEntry(LogLevel.Info,
                            $"DEBUG: OnCompleted — Success={result.Success}, " +
                            $"PickedCount={result.PickedDrainCount}, FinalCount={result.FinalDrainCount}"));

                        if (!result.Success)
                        {
                            AddLog(new LogEntry(LogLevel.Error,
                                $"AutoSlope failed: {result.ErrorMessage}"));
                            State = RunState.Ready;
                            return;
                        }

                        _lastResult       = result;
                        VerticesProcessed = result.VerticesProcessed;
                        VerticesSkipped   = result.VerticesSkipped;

                        AddLog(new LogEntry(LogLevel.Info,
                            $"DEBUG: About to set PickedDrainCount to {result.PickedDrainCount}"));
                        PickedDrainCount = result.PickedDrainCount;

                        AddLog(new LogEntry(LogLevel.Info,
                            $"DEBUG: About to set FinalDrainCount to {result.FinalDrainCount}"));
                        FinalDrainCount = result.FinalDrainCount;

                        AddLog(new LogEntry(LogLevel.Info,
                            $"DEBUG: After setting — PickedDrainCount={PickedDrainCount}, FinalDrainCount={FinalDrainCount}"));

                        HighestElevation_mm = result.HighestElevation_mm;
                        LongestPath_m       = result.LongestPath_m;
                        RunDuration_sec     = result.RunDuration_sec;
                        RunDate             = result.RunDate;

                        State = RunState.Done;
                        ExportResultsCommand.NotifyCanExecuteChanged();

                        if (!string.IsNullOrEmpty(result.ExportedFilePath))
                        {
                            var answer = MessageBox.Show(
                                "Excel file saved. Open it now?",
                                "Export Complete",
                                MessageBoxButton.YesNo,
                                MessageBoxImage.Question);

                            if (answer == MessageBoxResult.Yes)
                            {
                                AddLog(new LogEntry(LogLevel.Info,
                                    $"DEBUG: Opening file: '{result.ExportedFilePath}'"));
                                try
                                {
                                    System.Diagnostics.Process.Start(
                                        new System.Diagnostics.ProcessStartInfo
                                        {
                                            FileName        = result.ExportedFilePath,
                                            UseShellExecute = true
                                        });
                                }
                                catch (Exception ex)
                                {
                                    AddLog(new LogEntry(LogLevel.Warning,
                                        $"⚠ Could not open file: {ex.Message}"));
                                }
                            }
                        }

                        AddLog(new LogEntry(LogLevel.Info,
                            $"DEBUG: State set to Done — FinalDrainCount={FinalDrainCount}"));
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
                AddLog(new LogEntry(LogLevel.Info, $"Export folder set to: {ExportFolderPath}"));
            }
        }

        // ── ClearLog ──────────────────────────────────────────────────────────
        private void ClearLog()
        {
            LogEntries.Clear();
            AddLog(new LogEntry(LogLevel.Info, "Log cleared."));
        }

        // ── ExportResults ─────────────────────────────────────────────────────
        private void ExportResults()
        {
            if (_lastResult == null || !_lastResult.Success)
            {
                AddLog(new LogEntry(LogLevel.Warning, "Warning: Run AutoSlope successfully first."));
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
                AddLog);   // Action<LogEntry>

            if (!string.IsNullOrEmpty(savedPath))
            {
                AddLog(new LogEntry(LogLevel.Success, $"✅ Results exported to: {savedPath}"));

                var answer = MessageBox.Show(
                    "Excel file saved. Open it now?",
                    "Export Complete",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question);

                if (answer == MessageBoxResult.Yes)
                    System.Diagnostics.Process.Start(
                        new System.Diagnostics.ProcessStartInfo
                        {
                            FileName        = savedPath,
                            UseShellExecute = true
                        });
            }
        }

        // ── AddLog ────────────────────────────────────────────────────────────
        /// <summary>
        /// Thread-safe: if already on the UI thread, adds directly;
        /// otherwise dispatches via BeginInvoke.
        /// </summary>
        private void AddLog(LogEntry entry)
        {
            var dispatcher = Application.Current.Dispatcher;
            if (dispatcher.CheckAccess())
                LogEntries.Add(entry);
            else
                dispatcher.BeginInvoke(new Action(() => LogEntries.Add(entry)));
        }
    }
}
