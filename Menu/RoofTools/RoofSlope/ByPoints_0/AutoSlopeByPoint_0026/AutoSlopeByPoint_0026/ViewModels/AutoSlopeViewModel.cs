// =======================================================
// File: AutoSlopeViewModel.cs
// Namespace: Revit26_Plugin.AutoSlopeByPoint.V026
// Changes vs V06:
//   - LogText (string) replaced by LogEntries (ObservableCollection<LogEntry>)
//     from Revit26_Plugin.Shared.Models.
//   - AddLog(string) replaced by AddLog(LogEntry) — engine callback
//     now feeds structured entries directly.
//   - ExportResultsSummary log parameter updated to Action<LogEntry>.
//   - InverseBoolConverter → Revit26_Plugin.Shared.Models (no local copy).
//   - LogColorHelper removed — colour comes from LogLevelToColorConverter in XAML.
//   - ClearLog clears the ObservableCollection.
// Changes vs V025:
//   - Settings persistence expanded from Circle Markers only to ALL
//     user-changeable fields (slope %, threshold, drain tolerance +
//     enable flag, curve-intersection toggle, export folder, export
//     to Excel, and the new AskToOpenAfterExport). Renamed
//     LoadCircleMarkerSettings/SaveCircleMarkerSettings →
//     LoadAllSettings/SaveAllSettings to reflect the wider scope.
//     Window size/position is explicitly NOT persisted.
//   - New AskToOpenAfterExport bool property (default false) backing
//     the new "Ask to open file after export" checkbox. The Yes/No
//     "Open it now?" prompt — in both the Run-completion path and the
//     manual ExportResults path — now only fires when this is true;
//     previously it fired unconditionally whenever a file was exported.
//   - Circle marker default radius 500mm → 250mm (all 3 groups).
// =======================================================

using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using CommunityToolkit.Mvvm.Input;
using Revit26_Plugin.AutoSlopeByPoint.V026.Core.Models;
using Revit26_Plugin.AutoSlopeByPoint.V026.Infrastructure.ExternalEvents;
using Revit26_Plugin.AutoSlopeByPoint.V026.Infrastructure.Helpers;
using Revit26_Plugin.Shared.Models;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows;

namespace Revit26_Plugin.AutoSlopeByPoint.V026.UI.ViewModels
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

        private bool _insertCurveIntersectionPoints = true;
        public bool InsertCurveIntersectionPoints
        {
            get => _insertCurveIntersectionPoints;
            set { _insertCurveIntersectionPoints = value; Raise(); }
        }

        // ── Circle Markers (V026) ────────────────────────────────────────────
        /// <summary>Style/config for circles placed on final drain points.</summary>
        public CircleMarkerGroup DrainMarkerGroup { get; } = new CircleMarkerGroup
        {
            GroupLabel = "Drain",
            ColorName = "Blue",
            RadiusMm = 250
        };

        /// <summary>Style/config for circles placed on vertices tied at max elevation.</summary>
        public CircleMarkerGroup HighestPointMarkerGroup { get; } = new CircleMarkerGroup
        {
            GroupLabel = "Highest Point",
            ColorName = "Red",
            RadiusMm = 250
        };

        /// <summary>Style/config for circles placed on processed vertices meeting the Allowed Offset threshold.</summary>
        public CircleMarkerGroup AllowedOffsetMarkerGroup { get; } = new CircleMarkerGroup
        {
            GroupLabel = "Allowed Offset",
            ColorName = "Orange",
            RadiusMm = 250
        };

        private double _allowedOffsetThresholdMm = 500;
        public double AllowedOffsetThresholdMm
        {
            get => _allowedOffsetThresholdMm;
            set { _allowedOffsetThresholdMm = value; Raise(); }
        }

        /// <summary>Named colors offered in each group's Color dropdown.</summary>
        public IReadOnlyList<string> ColorPalette { get; } = NamedColorHelper.PaletteNames;

        /// <summary>
        /// Project's existing Line Style options (OST_Lines subcategories),
        /// populated in the constructor via FilteredElementCollector. The tool
        /// never creates new line styles — user picks from what already exists.
        /// </summary>
        public ObservableCollection<LineStyleOption> LineStyleOptions { get; } = new ObservableCollection<LineStyleOption>();

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
            set { _exportToExcel = value; Raise(); Raise(nameof(CanAskToOpenAfterExport)); }
        }

        /// <summary>
        /// When true, a successful Excel export (Run-completion path or manual
        /// Export) prompts the user with a Yes/No "Open it now?" dialog.
        /// When false (default), the exported file is never opened automatically
        /// and no prompt is shown. Disabled in the UI unless ExportToExcel is on.
        /// </summary>
        private bool _askToOpenAfterExport = false;
        public bool AskToOpenAfterExport
        {
            get => _askToOpenAfterExport;
            set { _askToOpenAfterExport = value; Raise(); }
        }

        /// <summary>UI-only gate: the "Ask to open" checkbox is enabled only when Export to Excel is on.</summary>
        public bool CanAskToOpenAfterExport => ExportToExcel;

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
            set { _runDuration_sec = value; Raise(); Raise(nameof(RunDurationDisplay)); Raise(nameof(RunDuration_ms)); }
        }
        // Displayed in milliseconds: whole-seconds value from the engine × 1000
        // (quick display-only conversion — not true ms-precision timing).
        public string RunDurationDisplay => $"{RunDuration_ms} ms";
        public int RunDuration_ms => RunDuration_sec * 1000;

        private int _curvesCalculated;
        public int CurvesCalculated
        {
            get => _curvesCalculated;
            set { _curvesCalculated = value; Raise(); }
        }

        private string _runDate;
        public string RunDate
        {
            get => _runDate;
            set { _runDate = value; Raise(); Raise(nameof(SummaryText)); }
        }

        // ── Circle Marker result counts (V026) ───────────────────────────────
        private int _drainCirclesPlaced;
        public int DrainCirclesPlaced
        {
            get => _drainCirclesPlaced;
            set { _drainCirclesPlaced = value; Raise(); Raise(nameof(SummaryText)); }
        }

        private int _highestCirclesPlaced;
        public int HighestCirclesPlaced
        {
            get => _highestCirclesPlaced;
            set { _highestCirclesPlaced = value; Raise(); Raise(nameof(SummaryText)); }
        }

        private int _offsetCirclesPlaced;
        public int OffsetCirclesPlaced
        {
            get => _offsetCirclesPlaced;
            set { _offsetCirclesPlaced = value; Raise(); Raise(nameof(SummaryText)); }
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
Export Folder            : {ExportFolderPath}
Circles Placed           : {DrainCirclesPlaced} drain / {HighestCirclesPlaced} highest / {OffsetCirclesPlaced} offset";

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

            LoadLineStyleOptions();
            LoadAllSettings();

            AutoSlopeEventManager.Init();
        }

        // ── LoadLineStyleOptions ─────────────────────────────────────────────
        /// <summary>
        /// Populates LineStyleOptions from the project's existing OST_Lines
        /// subcategories (GraphicsStyle elements) — the same list Revit shows
        /// in its own "Line Style" dropdowns. No new line styles are created.
        /// </summary>
        private void LoadLineStyleOptions()
        {
            LineStyleOptions.Clear();

            Document doc = UIDoc?.Document;
            if (doc == null) return;

            Category linesCategory = doc.Settings.Categories.get_Item(BuiltInCategory.OST_Lines);
            if (linesCategory?.SubCategories == null) return;

            foreach (Category sub in linesCategory.SubCategories)
            {
                if (sub?.GetGraphicsStyle(GraphicsStyleType.Projection) is GraphicsStyle gs)
                {
                    LineStyleOptions.Add(new LineStyleOption { Id = gs.Id, Name = sub.Name });
                }
            }

            // Sensible default: "Thin Lines" if present (Revit's built-in default
            // line style), otherwise the first available option.
            var defaultOption = LineStyleOptions.FirstOrDefault(o => o.Name == "Thin Lines")
                                 ?? LineStyleOptions.FirstOrDefault();

            if (defaultOption != null)
            {
                foreach (var group in new[] { DrainMarkerGroup, HighestPointMarkerGroup, AllowedOffsetMarkerGroup })
                {
                    if (group.LineStyleId == null)
                    {
                        group.LineStyleId = defaultOption.Id;
                        group.LineStyleName = defaultOption.Name;
                    }
                }
            }
            else
            {
                AddLog(new LogEntry(LogLevel.Warning,
                    "Circle Markers: no Line Styles found in this project (OST_Lines has no subcategories)."));
            }
        }

        // ── LoadAllSettings ───────────────────────────────────────────────────
        /// <summary>
        /// Applies ALL persisted settings (settings.json) onto the ViewModel:
        /// the 3 Circle Marker groups, the offset threshold, the run-input
        /// fields (slope %, threshold, drain tolerance, curve-intersection
        /// toggle), and the export fields (folder, export-to-Excel,
        /// ask-to-open-after-export). Must run AFTER LoadLineStyleOptions,
        /// since the persisted LineStyleName is matched against the freshly
        /// loaded LineStyleOptions — a name from a previous project that no
        /// longer exists here simply leaves the constructor's default line
        /// style in place. Window size/position is intentionally not part
        /// of this scope.
        /// </summary>
        private void LoadAllSettings()
        {
            AutoSlopeSettings settings = SettingsService.Load();

            ApplySettings(DrainMarkerGroup, settings.DrainMarkerGroup);
            ApplySettings(HighestPointMarkerGroup, settings.HighestPointMarkerGroup);
            ApplySettings(AllowedOffsetMarkerGroup, settings.AllowedOffsetMarkerGroup);
            AllowedOffsetThresholdMm = settings.AllowedOffsetThresholdMm;

            SlopePercent = settings.SlopePercent;
            ThresholdMeters = settings.ThresholdMeters;
            EnableDrainTolerance = settings.EnableDrainTolerance;
            DrainToleranceMm = settings.DrainToleranceMm;
            InsertCurveIntersectionPoints = settings.InsertCurveIntersectionPoints;

            // Export folder: only override the MyDocuments default set above
            // if a previously-saved path exists (first-run has none).
            if (!string.IsNullOrWhiteSpace(settings.ExportFolderPath))
                ExportFolderPath = settings.ExportFolderPath;

            ExportToExcel = settings.ExportToExcel;
            AskToOpenAfterExport = settings.AskToOpenAfterExport;
        }

        private void ApplySettings(CircleMarkerGroup group, CircleMarkerGroupSettings saved)
        {
            if (group == null || saved == null) return;

            group.IsEnabled = saved.IsEnabled;
            group.ColorName = string.IsNullOrWhiteSpace(saved.ColorName) ? group.ColorName : saved.ColorName;
            group.RadiusMm = saved.RadiusMm > 0 ? saved.RadiusMm : group.RadiusMm;

            if (!string.IsNullOrWhiteSpace(saved.LineStyleName))
            {
                var match = LineStyleOptions.FirstOrDefault(o => o.Name == saved.LineStyleName);
                if (match != null)
                {
                    group.LineStyleId = match.Id;
                    group.LineStyleName = match.Name;
                }
            }
        }

        // ── SaveAllSettings ───────────────────────────────────────────────────
        /// <summary>
        /// Persists ALL user-changeable fields to settings.json: the 3 marker
        /// groups + offset threshold, the run inputs, and the export fields.
        /// Called after a completed Run and again on window close, so the
        /// last values used — whichever came last — are what's remembered.
        /// Best-effort — failures are logged, not thrown. Public: also
        /// invoked from AutoSlopeWindow.OnClosing (code-behind).
        /// </summary>
        public void SaveAllSettings()
        {
            var settings = new AutoSlopeSettings
            {
                DrainMarkerGroup = ToSettings(DrainMarkerGroup),
                HighestPointMarkerGroup = ToSettings(HighestPointMarkerGroup),
                AllowedOffsetMarkerGroup = ToSettings(AllowedOffsetMarkerGroup),
                AllowedOffsetThresholdMm = AllowedOffsetThresholdMm,

                SlopePercent = SlopePercent,
                ThresholdMeters = ThresholdMeters,
                EnableDrainTolerance = EnableDrainTolerance,
                DrainToleranceMm = DrainToleranceMm,
                InsertCurveIntersectionPoints = InsertCurveIntersectionPoints,

                ExportFolderPath = ExportFolderPath,
                ExportToExcel = ExportToExcel,
                AskToOpenAfterExport = AskToOpenAfterExport
            };

            if (!SettingsService.Save(settings))
            {
                AddLog(new LogEntry(LogLevel.Warning, "Could not save settings."));
            }
        }

        private static CircleMarkerGroupSettings ToSettings(CircleMarkerGroup group) => new CircleMarkerGroupSettings
        {
            IsEnabled = group.IsEnabled,
            LineStyleName = group.LineStyleName,
            ColorName = group.ColorName,
            RadiusMm = group.RadiusMm
        };

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
                InsertCurveIntersectionPoints = InsertCurveIntersectionPoints,
                ProjectTitle       = UIDoc?.Document?.Title ?? "Unknown Project",
                Log                = AddLog,      // Action<LogEntry>
                ExportConfig       = new ExportConfig
                {
                    ExportPath           = ExportFolderPath,
                    ExportToExcel        = ExportToExcel,
                    IncludeVertexDetails = false
                },
                DrainMarkerGroup         = DrainMarkerGroup,
                HighestPointMarkerGroup  = HighestPointMarkerGroup,
                AllowedOffsetMarkerGroup = AllowedOffsetMarkerGroup,
                AllowedOffsetThresholdMm = AllowedOffsetThresholdMm,

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
                        CurvesCalculated    = result.CurvesCalculated;

                        DrainCirclesPlaced   = result.DrainCirclesPlaced;
                        HighestCirclesPlaced = result.HighestCirclesPlaced;
                        OffsetCirclesPlaced  = result.OffsetCirclesPlaced;

                        SaveAllSettings();

                        State = RunState.Done;
                        ExportResultsCommand.NotifyCanExecuteChanged();

                        if (!string.IsNullOrEmpty(result.ExportedFilePath) && AskToOpenAfterExport)
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

                if (AskToOpenAfterExport)
                {
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
