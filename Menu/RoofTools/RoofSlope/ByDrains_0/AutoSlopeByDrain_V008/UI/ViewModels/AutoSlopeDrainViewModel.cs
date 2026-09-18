// File: AutoSlopeDrainViewModel.cs
// Location: UI/ViewModels/
// Base: AutoSlopeByDrain V004's AutoSlopeDrainViewModel (Select All/None/
// Invert, size filter + DataGrid sorting via ICollectionView, Run wiring
// through AutoSlopeDrainEventManager).
//
// CHANGES (V005), per Rafi's confirmed decisions on 2026-07-21:
//   REWRITTEN using CommunityToolkit.Mvvm's ObservableObject +
//     [ObservableProperty] + [RelayCommand] source-generator attributes,
//     instead of the hand-rolled INotifyPropertyChanged + plain
//     RelayCommand pattern both V004 and ByPoint V018 actually used.
//   ADDED   ThresholdMeters (Max Path Distance) input, distinct from
//     ConnectionThresholdMeters (Max Edge Distance) — ported from ByPoint.
//   ADDED   InsertCurveIntersectionPoints toggle (off by default).
//   ADDED   VerifyElevationsAfterCommit toggle (OFF by default).
//   CHANGED export: Excel-only (ExportToExcel replaces ExportToCsv),
//     single exported file path instead of Detailed+Summary pair.
//   ADDED   settings persistence via SettingsService.
//   ADDED   CanRun / RunDisabledReason surfaced so the View can show an
//     inline hint when Run is disabled because 0 drains are selected.
//   ADDED   TotalDetectedCount / SelectedCount / ArcsCalculated metric
//     bindings for the metric cards.
//
// REWRITTEN (V008), per Rafi's confirmed multi-roof decision (2026-09-08):
//   The single RoofData + AllDrains/FilteredDrainsView/SizeFilters/
//   RoofSubtitle block moved out entirely into RoofTabViewModel — one
//   instance per roof the user picked, displayed as one TabItem in the
//   window (see AutoSlopeByDrainWindow.xaml). This ViewModel now holds
//   ObservableCollection<RoofTabViewModel> RoofTabs and keeps only the
//   settings that are SHARED across every roof in the run (slope %,
//   thresholds, marker styles, export folder) per Rafi's confirmed
//   decision to keep those global rather than per-roof.
//   RunAutoSlope now builds one AutoSlopeDrainPayload per roof tab that has
//   at least one selected drain (tabs with 0 selected are skipped with a
//   log line, not treated as an error) and raises them together as an
//   AutoSlopeDrainMultiPayload, so every roof runs inside one combined
//   Undo entry. The top metric cards now show BATCH totals: drains
//   selected/detected are summed across tabs, longest path / highest
//   elevation are the max across roofs that ran, run duration is summed.
//   Each roof's own numbers still live on its RoofTabViewModel
//   (LongestPath_m, HighestElevation_mm, ExportedFilePath, LastRunStatus)
//   for display inside that roof's own tab.

using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Revit26_Plugin.MultiRoofSlopeByDrain.Core.Models;
using Revit26_Plugin.MultiRoofSlopeByDrain.Infrastructure.ExternalEvents;
using Revit26_Plugin.MultiRoofSlopeByDrain.Infrastructure.Helpers;
using Revit26_Plugin.Shared.Models;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading;
using System.Windows;

namespace Revit26_Plugin.MultiRoofSlopeByDrain.UI.ViewModels
{
    public partial class AutoSlopeDrainViewModel : ObservableObject
    {
        // Must be public (not private) — [ObservableProperty] generates a public
        // State property, and a public property cannot expose a less-accessible
        // type. The enum is still only meant for internal use within this
        // ViewModel; nothing outside binds to it directly.
        // NEW: Cancelled added, per Rafi's confirmed Cancel decision.
        public enum RunState { Ready, Running, Done, Cancelled }

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(StatusMessage))]
        [NotifyPropertyChangedFor(nameof(LongestPathDisplay))]
        [NotifyPropertyChangedFor(nameof(HighestElevationDisplay))]
        [NotifyCanExecuteChangedFor(nameof(RunAutoSlopeCommand))]
        [NotifyCanExecuteChangedFor(nameof(CancelRunCommand))]
        private RunState state = RunState.Ready;

        private bool IsRunning => State == RunState.Running;
        private bool IsComplete => State == RunState.Done;

        /// <summary>NEW. Done OR Cancelled — used only to gate display of real numbers (metrics/timing); NOT used for Run-button re-enablement (see IsComplete), so cancelling never permanently locks the Run button.</summary>
        private bool IsFinished => State == RunState.Done || State == RunState.Cancelled;

        public string StatusMessage => State switch
        {
            RunState.Running => "Processing...",
            RunState.Done => "Completed",
            RunState.Cancelled => "Cancelled",
            _ => "Ready"
        };

        // ── Roofs ─────────────────────────────────────────────────────────────
        public UIDocument UIDoc { get; }
        public UIApplication App { get; }

        /// <summary>NEW (V008). One tab per roof the user picked in the Command.</summary>
        public ObservableCollection<RoofTabViewModel> RoofTabs { get; } = new ObservableCollection<RoofTabViewModel>();

        [ObservableProperty]
        private RoofTabViewModel selectedRoofTab;

        /// <summary>
        /// NEW (V008). Selecting a roof's tab also selects that roof in the model
        /// and zoom-fits it in the active view — per Rafi's confirmed decision so
        /// switching tabs keeps the plan view focused on whichever roof's grid is
        /// on screen. Best-effort: a roof that no longer resolves (e.g. deleted
        /// mid-session) logs a warning instead of throwing.
        /// </summary>
        partial void OnSelectedRoofTabChanged(RoofTabViewModel value)
        {
            if (value == null || UIDoc == null) return;

            try
            {
                var ids = new List<ElementId> { value.RoofId };
                UIDoc.Selection.SetElementIds(ids);
                UIDoc.ShowElements(ids);
            }
            catch (Exception ex)
            {
                AddLog(new LogEntry(LogLevel.Warning, $"Could not focus view on {value.RoofName}: {ex.Message}"));
            }
        }

        /// <summary>NEW (V008). Summary line shown in the App Identity card, e.g. "3 roofs selected".</summary>
        public string RoofSelectionSummary => RoofTabs.Count == 1
            ? RoofTabs[0].RoofSubtitle
            : $"{RoofTabs.Count} roofs selected";

        // ── Slope inputs — SHARED across every roof in the run ─────────────────
        public List<string> SlopeOptions { get; } = new List<string> { "1.0", "1.5", "2.0", "2.5", "3.0" };

        [ObservableProperty]
        private string slopeInput = AppConstants.DefaultSlopePercent.ToString("0.0");

        [ObservableProperty]
        private string connectionThresholdInput = AppConstants.DefaultConnectionThresholdMeters.ToString();

        /// <summary>Max Path Distance input, ported from ByPoint. Distinct from ConnectionThresholdInput.</summary>
        [ObservableProperty]
        private string thresholdInput = AppConstants.DefaultThresholdMeters.ToString();

        [ObservableProperty]
        private string pathSampleCountInput = AppConstants.DefaultPathSampleCount.ToString();

        /// <summary>Opt-in, off by default.</summary>
        [ObservableProperty]
        private bool insertCurveIntersectionPoints = false;

        /// <summary>Opt-in, OFF by default per Rafi's confirmed decision.</summary>
        [ObservableProperty]
        private bool verifyElevationsAfterCommit = false;

        // ── Circle Markers — ported from AutoSlopeByPoint, shared across roofs ─
        public CircleMarkerGroup DrainMarkerGroup { get; } = new CircleMarkerGroup
        {
            GroupLabel = "Drain",
            ColorName = "Blue",
            RadiusMm = 250
        };

        public CircleMarkerGroup HighestPointMarkerGroup { get; } = new CircleMarkerGroup
        {
            GroupLabel = "Highest Point",
            ColorName = "Red",
            RadiusMm = 250
        };

        public CircleMarkerGroup AllowedOffsetMarkerGroup { get; } = new CircleMarkerGroup
        {
            GroupLabel = "Allowed Offset",
            ColorName = "Orange",
            RadiusMm = 250
        };

        [ObservableProperty]
        private double allowedOffsetThresholdMm = 500;

        public IReadOnlyList<string> ColorPalette { get; } = NamedColorHelper.PaletteNames;

        public ObservableCollection<LineStyleOption> LineStyleOptions { get; } = new ObservableCollection<LineStyleOption>();

        // ── Export ────────────────────────────────────────────────────────────
        [ObservableProperty]
        private string exportFolderPath;

        /// <summary>NEW (V008). When more than one roof runs, also write a combined summary workbook comparing every roof.</summary>
        [ObservableProperty]
        private bool writeCombinedSummary = true;

        // ── Log ───────────────────────────────────────────────────────────────
        public ObservableCollection<LogEntry> LogEntries { get; } = new ObservableCollection<LogEntry>();

        // ── Results / metrics — BATCH totals across every roof that ran ────────
        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(LongestPathDisplay))]
        private double longestPath_m;
        public string LongestPathDisplay => IsFinished ? LongestPath_m.ToString("F2") : "N/A";

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(HighestElevationDisplay))]
        private double highestElevation_mm;
        public string HighestElevationDisplay => IsFinished ? HighestElevation_mm.ToString("F0") : "N/A";

        [ObservableProperty]
        private int runDuration_sec;

        // ── Progress / Cancel — NEW, per Rafi's confirmed decisions ────────────
        // One continuous bar spans the whole batch, pre-weighted by each roof's
        // vertex count (known upfront, no extra Revit calls) so a big roof takes
        // proportionally more of the bar than a small one. Updated from
        // AutoSlopeDrainPayload.Progress callbacks — see RunAutoSlope below.
        [ObservableProperty]
        private bool isProgressVisible;

        [ObservableProperty]
        private double progressPercent;

        [ObservableProperty]
        private bool progressIsIndeterminate = true;

        [ObservableProperty]
        private string progressPhaseText = "";

        private CancellationTokenSource _runCts;

        private bool CanCancelRun() => State == RunState.Running;

        [RelayCommand(CanExecute = nameof(CanCancelRun))]
        private void CancelRun()
        {
            if (_runCts == null || _runCts.IsCancellationRequested) return;
            _runCts.Cancel();
            ProgressPhaseText = "Cancelling…";
            AddLog(new LogEntry(LogLevel.Warning, "Cancel requested — finishing the current roof's in-progress work, then stopping."));
        }

        [ObservableProperty]
        private int finalDrainCount;

        /// <summary>Sum of drains DrainDetectionService found across every roof tab, before selection.</summary>
        [ObservableProperty]
        private int totalDetectedCount;

        /// <summary>Sum of drains checked across every roof tab at Run time.</summary>
        [ObservableProperty]
        private int selectedCount;

        /// <summary>Sum of boundary/opening arcs found across every roof that ran. 0 unless InsertCurveIntersectionPoints was on.</summary>
        [ObservableProperty]
        private int arcsCalculated;

        [ObservableProperty]
        private int drainCirclesPlaced;

        [ObservableProperty]
        private int highestCirclesPlaced;

        [ObservableProperty]
        private int offsetCirclesPlaced;

        /// <summary>NEW (V008): roofs actually processed (had >=1 selected drain) in the last run.</summary>
        [ObservableProperty]
        private int roofsProcessed;

        /// <summary>True only when the Run button is disabled specifically because 0 drains are selected anywhere across all roof tabs.</summary>
        public bool ShowNoDrainsSelectedHint => !IsRunning && !IsComplete && RoofTabs.All(t => t.SelectedDrainsCount == 0);

        /// <summary>NEW (V008): drains checked right now, summed across every roof tab — bound to the "Drains Selected" metric card.</summary>
        public int AllSelectedDrainsCount => RoofTabs.Sum(t => t.SelectedDrainsCount);

        // ── Constructor ───────────────────────────────────────────────────────
        public AutoSlopeDrainViewModel(UIDocument uidoc, UIApplication app, List<RoofData> roofDataList)
        {
            UIDoc = uidoc;
            App = app;

            int tabIndex = 1;
            foreach (var roofData in roofDataList)
            {
                var tab = new RoofTabViewModel(roofData, tabIndex++);
                tab.SelectionChanged += OnAnyDrainSelectionChanged;
                RoofTabs.Add(tab);
            }
            SelectedRoofTab = RoofTabs.FirstOrDefault();

            TotalDetectedCount = RoofTabs.Sum(t => t.TotalDetectedCount);

            LoadLineStyleOptions();

            // Load persisted settings, falling back to AppConstants defaults for
            // anything not yet saved (first run, or a fresh machine).
            var settings = SettingsService.Load(msg => AddLog(new LogEntry(LogLevel.Warning, msg)));

            SlopeInput = settings.SlopePercent.ToString("0.0");
            ConnectionThresholdInput = settings.ConnectionThresholdMeters.ToString("0");
            ThresholdInput = settings.ThresholdMeters.ToString("0");
            PathSampleCountInput = settings.PathSampleCount.ToString();
            InsertCurveIntersectionPoints = settings.InsertCurveIntersectionPoints;
            VerifyElevationsAfterCommit = settings.VerifyElevationsAfterCommit;

            string savedFilter = string.IsNullOrEmpty(settings.SelectedSizeFilter) ? "All" : settings.SelectedSizeFilter;
            foreach (var tab in RoofTabs)
                if (tab.SizeFilters.Contains(savedFilter))
                    tab.SelectedSizeFilter = savedFilter;

            ApplyMarkerSettings(DrainMarkerGroup, settings.DrainMarkerGroup);
            ApplyMarkerSettings(HighestPointMarkerGroup, settings.HighestPointMarkerGroup);
            ApplyMarkerSettings(AllowedOffsetMarkerGroup, settings.AllowedOffsetMarkerGroup);
            AllowedOffsetThresholdMm = settings.AllowedOffsetThresholdMm;

            ExportFolderPath = !string.IsNullOrWhiteSpace(settings.ExportFolderPath) && Directory.Exists(settings.ExportFolderPath)
                ? settings.ExportFolderPath
                : Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                    AppConstants.DefaultExportFolder);

            if (!Directory.Exists(ExportFolderPath))
                Directory.CreateDirectory(ExportFolderPath);

            AddLog(new LogEntry(LogLevel.Info,
                RoofTabs.Count == 1
                    ? $"Detected {RoofTabs[0].TotalDetectedCount} drain opening(s)."
                    : $"{RoofTabs.Count} roofs selected — detected {TotalDetectedCount} drain opening(s) total."));
            AddLog(new LogEntry(LogLevel.Info, $"Default export folder: {ExportFolderPath}"));

            AutoSlopeDrainEventManager.Init();
        }

        private void OnAnyDrainSelectionChanged()
        {
            OnPropertyChanged(nameof(ShowNoDrainsSelectedHint));
            OnPropertyChanged(nameof(AllSelectedDrainsCount));
            RunAutoSlopeCommand.NotifyCanExecuteChanged();
        }

        // ── LoadLineStyleOptions ─────────────────────────────────────────────
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

        private void ApplyMarkerSettings(CircleMarkerGroup group, CircleMarkerGroupSettings saved)
        {
            if (group == null || saved == null) return;

            group.IsEnabled = saved.IsEnabled;
            group.ColorName = string.IsNullOrWhiteSpace(saved.ColorName) ? group.ColorName : saved.ColorName;
            group.RadiusMm = saved.RadiusMm > 0 ? saved.RadiusMm : group.RadiusMm;
            group.ShowOffsetText = saved.ShowOffsetText;

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

        private static CircleMarkerGroupSettings ToMarkerSettings(CircleMarkerGroup group) => new CircleMarkerGroupSettings
        {
            IsEnabled = group.IsEnabled,
            LineStyleName = group.LineStyleName,
            ColorName = group.ColorName,
            RadiusMm = group.RadiusMm,
            ShowOffsetText = group.ShowOffsetText
        };

        // ── Run ───────────────────────────────────────────────────────────────
        private bool CanRunAutoSlope() => !IsRunning && !IsComplete && RoofTabs.Any(t => t.SelectedDrainsCount > 0);

        [RelayCommand(CanExecute = nameof(CanRunAutoSlope))]
        private void RunAutoSlope()
        {
            if (IsRunning || IsComplete) return;

            if (!double.TryParse(SlopeInput, out double slopePercent) || slopePercent <= 0)
            {
                AddLog(new LogEntry(LogLevel.Error, "Please enter a valid positive slope percentage."));
                return;
            }
            if (!double.TryParse(ConnectionThresholdInput, out double connectionThresholdM) || connectionThresholdM <= 0)
            {
                AddLog(new LogEntry(LogLevel.Error, "Please enter a valid positive Max Edge Distance (m)."));
                return;
            }
            if (!double.TryParse(ThresholdInput, out double thresholdM) || thresholdM <= 0)
            {
                AddLog(new LogEntry(LogLevel.Error, "Please enter a valid positive Max Path Distance (m)."));
                return;
            }
            if (!int.TryParse(PathSampleCountInput, out int pathSamples) || pathSamples < 2)
            {
                AddLog(new LogEntry(LogLevel.Error, "Path Samples must be a whole number of 2 or more."));
                return;
            }

            var runnableTabs = RoofTabs.Where(t => t.SelectedDrainsCount > 0).ToList();
            if (runnableTabs.Count == 0)
            {
                // Defensive backstop — CanRunAutoSlope already prevents this in
                // normal use via the disabled Run button + inline hint.
                AddLog(new LogEntry(LogLevel.Warning, "No drains selected on any roof."));
                return;
            }

            var overallStartTime = DateTime.Now;

            State = RunState.Running;
            LogEntries.Clear();

            // NEW: fresh CancellationTokenSource shared by every roof payload
            // in this batch, so Cancel stops the whole run, not just one roof. The
            // bar starts indeterminate ("Starting…") since nothing has reported yet.
            _runCts = new CancellationTokenSource();
            IsProgressVisible = true;
            ProgressPercent = 0;
            ProgressIsIndeterminate = true;
            ProgressPhaseText = "Starting…";
            // Note: State's setter already fires CancelRunCommand.NotifyCanExecuteChanged()
            // via [NotifyCanExecuteChangedFor] above, so the Cancel button enables itself.

            AddLog(new LogEntry(LogLevel.Info,
                runnableTabs.Count == 1
                    ? $"Starting AutoSlope By Drain at {overallStartTime:HH:mm:ss}..."
                    : $"Starting AutoSlope By Drain on {runnableTabs.Count} of {RoofTabs.Count} selected roof(s) at {overallStartTime:HH:mm:ss}..."));

            foreach (var skipped in RoofTabs.Except(runnableTabs))
                AddLog(new LogEntry(LogLevel.Warning, $"[{skipped.RoofName}] Skipped — no drains selected."));

            if (!Directory.Exists(ExportFolderPath))
            {
                try { Directory.CreateDirectory(ExportFolderPath); }
                catch (Exception ex)
                {
                    AddLog(new LogEntry(LogLevel.Warning, $"Failed to create export directory: {ex.Message}"));
                }
            }

            string projectTitle = UIDoc?.Document?.Title ?? "Unknown Project";
            var roofPayloads = new List<AutoSlopeDrainPayload>();

            // NEW, per Rafi's confirmed multi-roof decision: one continuous
            // progress bar spans the whole batch, weighted by each roof's shape-edit
            // vertex count — the actual driver of BuildGraph's O(n^2) cost — so a big
            // roof takes proportionally more of the bar than a small one. Math.Max(1, …)
            // guards a roof with zero vertices (shouldn't happen, but avoids div-by-zero).
            double totalWeight = runnableTabs.Sum(t => (double)Math.Max(1, t.RoofData.Vertices.Count));
            double weightDoneSoFar = 0;

            foreach (var tab in runnableTabs)
            {
                RoofTabViewModel capturedTab = tab;
                double roofWeight = Math.Max(1, capturedTab.RoofData.Vertices.Count);
                double weightBeforeThisRoof = weightDoneSoFar;
                weightDoneSoFar += roofWeight;

                roofPayloads.Add(new AutoSlopeDrainPayload
                {
                    RoofId = tab.RoofId,
                    RoofName = tab.RoofName,
                    SelectedDrains = tab.GetSelectedDrains(),
                    TotalDetectedCount = tab.TotalDetectedCount,
                    SlopePercent = slopePercent,
                    ConnectionThresholdMeters = connectionThresholdM,
                    ThresholdMeters = thresholdM,
                    PathSampleCount = pathSamples,
                    InsertCurveIntersectionPoints = InsertCurveIntersectionPoints,
                    VerifyElevationsAfterCommit = VerifyElevationsAfterCommit,
                    DrainMarkerGroup = DrainMarkerGroup,
                    HighestPointMarkerGroup = HighestPointMarkerGroup,
                    AllowedOffsetMarkerGroup = AllowedOffsetMarkerGroup,
                    AllowedOffsetThresholdMm = AllowedOffsetThresholdMm,
                    ProjectTitle = projectTitle,
                    Log = entry => AddLog(new LogEntry(entry.Level, $"[{capturedTab.RoofName}] {entry.Message}")),
                    ExportConfig = new ExportConfig
                    {
                        ExportPath = ExportFolderPath,
                        ExportToExcel = true
                    },
                    CancelToken = _runCts.Token,
                    // NEW: called synchronously on the SAME thread as this run
                    // (Revit's main thread, same thread the window lives on — see
                    // UiPumpHelper) directly from inside BuildGraph/RoofSlopeProcessorService,
                    // never via BeginInvoke — the whole point is to update the properties
                    // and then immediately force WPF to actually repaint + process a
                    // queued Cancel click, both of which BeginInvoke would just defer.
                    Progress = info =>
                    {
                        string roofPrefix = runnableTabs.Count > 1 ? $"Roof {capturedTab.TabHeader.Replace("Roof ", "")} — " : "";
                        ProgressPhaseText = roofPrefix + info.PhaseLabel;

                        if (info.PercentWithinPhase.HasValue)
                        {
                            ProgressIsIndeterminate = false;
                            double overall = (weightBeforeThisRoof + roofWeight * (info.PercentWithinPhase.Value / 100.0)) / totalWeight * 100.0;
                            ProgressPercent = Math.Max(ProgressPercent, Math.Min(100.0, overall));
                        }
                        else
                        {
                            ProgressIsIndeterminate = true;
                        }

                        UiPumpHelper.DoEvents();
                    },
                    OnCompleted = result =>
                    {
                        Application.Current.Dispatcher.BeginInvoke(new Action(() =>
                        {
                            capturedTab.ApplyRunResult(result);
                        }));
                    }
                });
            }

            AutoSlopeDrainHandler.MultiPayload = new AutoSlopeDrainMultiPayload
            {
                RoofPayloads = roofPayloads,
                WriteCombinedSummary = WriteCombinedSummary,
                ExportFolderPath = ExportFolderPath,
                ProjectTitle = projectTitle,
                Log = entry => AddLog(entry),
                OnAllCompleted = roofResults =>
                {
                    Application.Current.Dispatcher.BeginInvoke(new Action(() =>
                    {
                        var succeeded = roofResults.Where(r => r.Result?.Success == true).ToList();
                        bool wasCancelled = roofResults.Any(r => r.Result?.WasCancelled == true);

                        IsProgressVisible = false;
                        _runCts = null;

                        if (succeeded.Count == 0)
                        {
                            AddLog(new LogEntry(
                                wasCancelled ? LogLevel.Warning : LogLevel.Error,
                                wasCancelled ? "Run cancelled before any roof completed." : "Run failed on every roof."));
                            State = wasCancelled ? RunState.Cancelled : RunState.Ready;
                            return;
                        }

                        RoofsProcessed = roofResults.Count;
                        LongestPath_m = succeeded.Max(r => r.Result.LongestPath_m);
                        HighestElevation_mm = succeeded.Max(r => r.Result.HighestElevation_mm);
                        RunDuration_sec = roofResults.Sum(r => r.Result?.RunDuration_sec ?? 0);
                        FinalDrainCount = succeeded.Sum(r => r.Result.DrainCount);
                        TotalDetectedCount = roofResults.Sum(r => r.Result?.TotalDetectedCount ?? 0);
                        SelectedCount = succeeded.Sum(r => r.Result.SelectedCount);
                        ArcsCalculated = succeeded.Sum(r => r.Result.ArcsCalculated);
                        DrainCirclesPlaced = succeeded.Sum(r => r.Result.DrainCirclesPlaced);
                        HighestCirclesPlaced = succeeded.Sum(r => r.Result.HighestCirclesPlaced);
                        OffsetCirclesPlaced = succeeded.Sum(r => r.Result.OffsetCirclesPlaced);

                        if (wasCancelled)
                            AddLog(new LogEntry(LogLevel.Warning,
                                $"Cancelled — {succeeded.Count} of {RoofTabs.Count} roof(s) had already completed and stay applied; the rest were not run."));
                        else if (roofResults.Count > succeeded.Count)
                            AddLog(new LogEntry(LogLevel.Warning,
                                $"{roofResults.Count - succeeded.Count} of {roofResults.Count} roof(s) failed — see log above for details."));

                        var overallEndTime = DateTime.Now;
                        int overallDurationSec = (int)(overallEndTime - overallStartTime).TotalSeconds;
                        AddLog(new LogEntry(LogLevel.Info,
                            $"{(wasCancelled ? "Run cancelled" : "All roofs completed")} — Started {overallStartTime:HH:mm:ss} | Finished {overallEndTime:HH:mm:ss} | Total duration: {overallDurationSec}s"));

                        // State stays Cancelled (not Done) when the user cancelled, even
                        // though some roofs succeeded — per Rafi's confirmed Cancel-scope
                        // decision, this is deliberately NOT the same as IsComplete
                        // (RunState.Done), so the Run button does not get permanently
                        // disabled after a cancel — the user can adjust settings and retry.
                        State = wasCancelled ? RunState.Cancelled : RunState.Done;

                        SettingsService.Save(new AutoSlopeDrainSettings
                        {
                            SlopePercent = slopePercent,
                            ConnectionThresholdMeters = connectionThresholdM,
                            ThresholdMeters = thresholdM,
                            PathSampleCount = pathSamples,
                            InsertCurveIntersectionPoints = InsertCurveIntersectionPoints,
                            VerifyElevationsAfterCommit = VerifyElevationsAfterCommit,
                            ExportFolderPath = ExportFolderPath,
                            SelectedSizeFilter = SelectedRoofTab?.SelectedSizeFilter ?? "All",
                            DrainMarkerGroup = ToMarkerSettings(DrainMarkerGroup),
                            HighestPointMarkerGroup = ToMarkerSettings(HighestPointMarkerGroup),
                            AllowedOffsetMarkerGroup = ToMarkerSettings(AllowedOffsetMarkerGroup),
                            AllowedOffsetThresholdMm = AllowedOffsetThresholdMm
                        }, msg => AddLog(new LogEntry(LogLevel.Warning, msg)));

                        bool anyExported = succeeded.Any(r => !string.IsNullOrEmpty(r.Result.ExportedFilePath));
                        if (anyExported)
                        {
                            var answer = MessageBox.Show(
                                roofResults.Count > 1
                                    ? "Excel export completed for each roof. Open the export folder now?"
                                    : "Excel export completed. Open the export folder now?",
                                "Export Complete",
                                MessageBoxButton.YesNo,
                                MessageBoxImage.Question);

                            if (answer == MessageBoxResult.Yes)
                                System.Diagnostics.Process.Start("explorer.exe", ExportFolderPath);
                        }
                    }));
                }
            };

            AutoSlopeDrainEventManager.Event.Raise();
        }

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

        [RelayCommand]
        private void OpenExportFolder()
        {
            if (!string.IsNullOrEmpty(ExportFolderPath) && Directory.Exists(ExportFolderPath))
                System.Diagnostics.Process.Start("explorer.exe", ExportFolderPath);
        }

        [RelayCommand]
        private void ClearLog()
        {
            LogEntries.Clear();
            AddLog(new LogEntry(LogLevel.Info, "Log cleared."));
        }

        [RelayCommand]
        private void CopyAllLogs()
        {
            if (LogEntries.Count == 0) return;
            string text = string.Join(Environment.NewLine, LogEntries.Select(e => e.ToString()));
            Clipboard.SetText(text);
        }

        /// <summary>
        /// Called by the View's code-behind on window close, so settings capture
        /// the current UI state even if the user never clicked Run this session.
        /// </summary>
        public void SaveSettingsOnClose()
        {
            double.TryParse(SlopeInput, out double slope);
            double.TryParse(ConnectionThresholdInput, out double connThreshold);
            double.TryParse(ThresholdInput, out double threshold);
            int.TryParse(PathSampleCountInput, out int samples);

            SettingsService.Save(new AutoSlopeDrainSettings
            {
                SlopePercent = slope > 0 ? slope : AppConstants.DefaultSlopePercent,
                ConnectionThresholdMeters = connThreshold > 0 ? connThreshold : AppConstants.DefaultConnectionThresholdMeters,
                ThresholdMeters = threshold > 0 ? threshold : AppConstants.DefaultThresholdMeters,
                PathSampleCount = samples >= 2 ? samples : AppConstants.DefaultPathSampleCount,
                InsertCurveIntersectionPoints = InsertCurveIntersectionPoints,
                VerifyElevationsAfterCommit = VerifyElevationsAfterCommit,
                ExportFolderPath = ExportFolderPath,
                SelectedSizeFilter = SelectedRoofTab?.SelectedSizeFilter ?? "All",
                DrainMarkerGroup = ToMarkerSettings(DrainMarkerGroup),
                HighestPointMarkerGroup = ToMarkerSettings(HighestPointMarkerGroup),
                AllowedOffsetMarkerGroup = ToMarkerSettings(AllowedOffsetMarkerGroup),
                AllowedOffsetThresholdMm = AllowedOffsetThresholdMm
            });
        }

        // ── AddLog (thread-safe) ──────────────────────────────────────────────
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
