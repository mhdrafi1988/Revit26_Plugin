// =======================================================
// File: AutoSlopeViewModel.cs
// Namespace: Revit26_Plugin.AutoSlopeByPoint.V028
// Refactored to CommunityToolkit.Mvvm ([ObservableProperty]/[RelayCommand])
// per MasterGuide stack convention, matching CircleMarkerGroup.cs in this
// same tool. Same public API (property/command names unchanged, so XAML
// bindings are untouched) — INotifyPropertyChanged/RelayCommand backing
// fields are now source-generated instead of hand-written.
// =======================================================

using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Revit26_Plugin.AutoSlopeByPoint.V028.Core.Models;
using Revit26_Plugin.AutoSlopeByPoint.V028.Infrastructure.ExternalEvents;
using Revit26_Plugin.AutoSlopeByPoint.V028.Infrastructure.Helpers;
using Revit26_Plugin.Shared.Models;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Windows;

namespace Revit26_Plugin.AutoSlopeByPoint.V028.UI.ViewModels
{
    public partial class AutoSlopeViewModel : ObservableObject
    {
        // ── RunState ──────────────────────────────────────────────────────────
        private enum RunState { Ready, Running, Done }

        private RunState _state = RunState.Ready;
        private RunState State
        {
            get => _state;
            set
            {
                _state = value;
                OnPropertyChanged(nameof(StatusMessage));
                OnPropertyChanged(nameof(StatusColor));
                RunCommand.NotifyCanExecuteChanged();
                ExportResultsCommand.NotifyCanExecuteChanged();
            }
        }

        private bool IsRunning  => _state == RunState.Running;
        private bool IsComplete => _state == RunState.Done;

        // ── Slope options ─────────────────────────────────────────────────────
        public List<double> SlopeOptions { get; } = new List<double> { 0.5, 1.0, 1.5, 2.0, 2.5 };

        // ── Input properties ──────────────────────────────────────────────────
        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(AppliedSlopeDisplay))]
        private double slopePercent = AppConstants.DefaultSlopePercent;

        public string AppliedSlopeDisplay => $"{SlopePercent}%";

        [ObservableProperty]
        private int thresholdMeters = AppConstants.DefaultThresholdMeters;

        [ObservableProperty]
        private int drainToleranceMm = AppConstants.DefaultDrainToleranceMm;

        [ObservableProperty]
        private bool enableDrainTolerance = true;

        [ObservableProperty]
        private bool insertCurveIntersectionPoints = true;

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

        [ObservableProperty]
        private double allowedOffsetThresholdMm = 500;

        /// <summary>Named colors offered in each group's Color dropdown.</summary>
        public IReadOnlyList<string> ColorPalette { get; } = NamedColorHelper.PaletteNames;

        /// <summary>
        /// Project's existing Line Style options (OST_Lines subcategories),
        /// populated in the constructor via FilteredElementCollector. The tool
        /// never creates new line styles — user picks from what already exists.
        /// </summary>
        public ObservableCollection<LineStyleOption> LineStyleOptions { get; } = new ObservableCollection<LineStyleOption>();

        [ObservableProperty]
        private string exportFolderPath;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(CanAskToOpenAfterExport))]
        private bool exportToExcel = true;

        /// <summary>
        /// When true, a successful Excel export (Run-completion path or manual
        /// Export) prompts the user with a Yes/No "Open it now?" dialog.
        /// When false (default), the exported file is never opened automatically
        /// and no prompt is shown. Disabled in the UI unless ExportToExcel is on.
        /// </summary>
        [ObservableProperty]
        private bool askToOpenAfterExport = false;

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
        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(SummaryText))]
        private int verticesProcessed;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(SummaryText))]
        private int verticesSkipped;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(SummaryText))]
        private int pickedDrainCount;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(SummaryText))]
        private int finalDrainCount;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(HighestElevationDisplay))]
        private double highestElevation_mm;

        public string HighestElevationDisplay => $"{HighestElevation_mm:0} mm";

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(LongestPathDisplay))]
        private double longestPath_m;

        public string LongestPathDisplay => $"{LongestPath_m:0.00} m";

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(RunDurationDisplay))]
        [NotifyPropertyChangedFor(nameof(RunDuration_ms))]
        private int runDuration_sec;

        // Displayed in milliseconds: whole-seconds value from the engine × 1000
        // (quick display-only conversion — not true ms-precision timing).
        public string RunDurationDisplay => $"{RunDuration_ms} ms";
        public int RunDuration_ms => RunDuration_sec * 1000;

        [ObservableProperty]
        private int curvesCalculated;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(SummaryText))]
        private string runDate;

        // ── Circle Marker result counts (V026) ───────────────────────────────
        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(SummaryText))]
        private int drainCirclesPlaced;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(SummaryText))]
        private int highestCirclesPlaced;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(SummaryText))]
        private int offsetCirclesPlaced;

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
        /// invoked from AutoSlopeByPointWindow.OnClosing (code-behind).
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

        // ── Run ───────────────────────────────────────────────────────────────
        [RelayCommand(CanExecute = nameof(CanRun))]
        private void Run()
        {
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
                        PickedDrainCount  = result.PickedDrainCount;
                        FinalDrainCount   = result.FinalDrainCount;

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
                    }));
                }
            };

            AutoSlopeEventManager.Event.Raise();
        }

        private bool CanRun() => !IsRunning && !IsComplete;

        // ── BrowseFolder ──────────────────────────────────────────────────────
        [RelayCommand]
        private void BrowseFolder()
        {
            var selected = DialogService.SelectFolder(ExportFolderPath);
            if (!string.IsNullOrEmpty(selected))
            {
                ExportFolderPath = selected;
                AddLog(new LogEntry(LogLevel.Info, $"Export folder set to: {ExportFolderPath}"));
            }
        }

        // ── ClearLog ──────────────────────────────────────────────────────────
        [RelayCommand]
        private void ClearLog()
        {
            LogEntries.Clear();
            AddLog(new LogEntry(LogLevel.Info, "Log cleared."));
        }

        // ── ExportResults ─────────────────────────────────────────────────────
        [RelayCommand(CanExecute = nameof(CanExportResults))]
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

        private bool CanExportResults() => IsComplete && _lastResult?.Success == true;

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
