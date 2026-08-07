using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows;
using Microsoft.Win32;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Revit26_Plugin.Shared.Models;

namespace Revit26_Plugin.RoofFromDetailLines.V007
{
    /// <summary>Row shown in the Roof Loops Preview grid — one row per resulting roof
    /// (outer boundary + its openings grouped together), per confirmed spec.</summary>
    public class RoofPreviewRow
    {
        public int RoofNumber { get; set; }
        public string StatusLabel { get; set; }
        public string StatusKind { get; set; } // "Ready" | "Opening" | "AutoClosed" — for XAML style trigger
        public string LevelName { get; set; }
        public string BoundaryLineIds { get; set; }
        public string OpeningLineIds { get; set; }
        public double AreaSqM { get; set; }
        public RoofGroup Group { get; set; }
    }

    public partial class MainViewModel : ObservableObject
    {
        private readonly Document _doc;
        private readonly Autodesk.Revit.DB.View _activeView;
        private readonly Level _level;
        private readonly List<Element> _detailElements;
        private readonly RunCreateRoofsExternalEventHandler _handler;
        private readonly ExternalEvent _externalEvent;
        private readonly ToolSettings _settings;

        public ObservableCollection<RoofType> RoofTypes { get; } = new();
        public ObservableCollection<RoofPreviewRow> PreviewRows { get; } = new();
        public ObservableCollection<LogEntry> Logs { get; } = new();

        [ObservableProperty]
        [NotifyCanExecuteChangedFor(nameof(RunCommand))]
        private RoofType selectedRoofType;

        [ObservableProperty] private string cornerToleranceMmText = "100";
        [ObservableProperty] private bool deleteSourceLines;
        [ObservableProperty] private bool isBusy;

        /// <summary>Confirmed spec: Run disables permanently after a successful run and
        /// only re-enables via Refresh Preview — not the usual busy-only disable. Guards
        /// against re-running against a preview the user hasn't re-validated.</summary>
        [ObservableProperty]
        [NotifyCanExecuteChangedFor(nameof(RunCommand))]
        private bool hasRunSinceLastPreview;

        /// <summary>Roof Loops Preview grid starts expanded on launch so the user sees
        /// results immediately; collapses automatically after a successful Run since
        /// the grid reflects the pre-run preview, not the actual outcome (that's what
        /// the Element ID Outputs / Activity Log are for). Re-expands whenever the
        /// preview is rebuilt (Refresh Preview), since fresh data is worth reviewing.</summary>
        [ObservableProperty] private bool isLoopsPreviewExpanded = true;

        [ObservableProperty] private string activeViewName = "";
        [ObservableProperty] private string levelDisplay = "";
        [ObservableProperty] private int selectedLineCount;
        [ObservableProperty] private int selectedArcCount;
        [ObservableProperty] private int tinyElementCount;

        // ── Live preview metrics ────────────────────────────────────────────────
        [ObservableProperty] private int metricClosedLoops;
        [ObservableProperty] private int metricAutoClosed;
        [ObservableProperty] private int metricCornerTrimmed;
        [ObservableProperty] private int metricSkipped;
        [ObservableProperty] private int metricStrandedElements;
        [ObservableProperty] private int metricRoofsToCreate;
        [ObservableProperty] private int metricWithOpenings;

        // ── Copyable outputs ─────────────────────────────────────────────────────
        [ObservableProperty] private string autoClosedLinesText = "";
        [ObservableProperty] private string roofsFromAutoClosedText = "(populated after Run)";
        [ObservableProperty] private string skippedLinesText = "";
        [ObservableProperty] private string allCreatedRoofsText = "(populated after Run)";
        [ObservableProperty] private string roofsWithOpeningsText = "(populated after Run)";

        [ObservableProperty] private string summaryText = "";
        public bool HasSummary => !string.IsNullOrEmpty(SummaryText);
        partial void OnSummaryTextChanged(string value) => OnPropertyChanged(nameof(HasSummary));

        [ObservableProperty] private string toastMessage = "";
        public bool HasToast => !string.IsNullOrEmpty(ToastMessage);
        partial void OnToastMessageChanged(string value) => OnPropertyChanged(nameof(HasToast));

        // Cached last preview build, reused by Run so it doesn't recompute mid-transaction.
        private List<RoofGroup> _lastRoofGroups = new();

        public MainViewModel(
            Document doc,
            Autodesk.Revit.DB.View activeView,
            Level level,
            List<Element> detailElements,
            RunCreateRoofsExternalEventHandler handler,
            ExternalEvent externalEvent)
        {
            _doc = doc;
            _activeView = activeView;
            _level = level;
            _detailElements = detailElements;
            _handler = handler;
            _externalEvent = externalEvent;

            ActiveViewName = activeView.Name;
            LevelDisplay = $"{level.Name}  (Elev {level.Elevation * 304.8:0} mm)";
            SelectedLineCount = detailElements.Count;

            // Informational-only counts (do not affect loop processing behavior).
            const double tinyThresholdFeet = 10.0 / 304.8; // 10mm in feet
            SelectedArcCount = detailElements.Count(e => (e as CurveElement)?.GeometryCurve is Arc);
            TinyElementCount = detailElements.Count(e =>
                (e as CurveElement)?.GeometryCurve is Curve c && c.IsBound && c.Length < tinyThresholdFeet);

            _settings = SettingsService.Load(out string settingsMsg);
            AddLog(LogLevel.Info, settingsMsg);

            cornerToleranceMmText = _settings.CornerToleranceMm.ToString("0.###");
            deleteSourceLines = _settings.DeleteSourceLines;

            LoadRoofTypes();

            AddLog(LogLevel.Info, $"Command started — active view '{ActiveViewName}', type {activeView.ViewType} ✓");
            AddLog(LogLevel.Info, $"Attached level resolved: '{level.Name}' (elev {level.Elevation * 304.8:0}mm)");
            AddLog(LogLevel.Info, $"Pre-selection filtered to {detailElements.Count} DetailLine/DetailArc element(s) " +
                                   $"({SelectedArcCount} arc(s), {TinyElementCount} under 10mm).");

            RebuildPreview();
        }

        private void LoadRoofTypes()
        {
            var types = new FilteredElementCollector(_doc)
                .OfClass(typeof(RoofType))
                .Cast<RoofType>()
                .OrderBy(t => t.Name);

            foreach (var t in types) RoofTypes.Add(t);

            var restored = RoofTypes.FirstOrDefault(t => t.Name == _settings.LastRoofTypeName);
            SelectedRoofType = restored ?? RoofTypes.FirstOrDefault();

            AddLog(restored != null ? LogLevel.Info : LogLevel.Info,
                restored != null
                    ? $"Roof type restored from settings: '{restored.Name}' (id {restored.Id.Value})."
                    : RoofTypes.Count > 0
                        ? $"Default roof type selected: '{SelectedRoofType?.Name}' (id {SelectedRoofType?.Id.Value})."
                        : "No roof types found in host model.");

            if (RoofTypes.Count == 0)
                AddLog(LogLevel.Warning, "No RoofType elements found — Run will be unavailable.");
        }

        private double ToleranceFeet()
        {
            if (!double.TryParse(CornerToleranceMmText, out double mm) || mm < 0) return 0.0;
            return UnitUtils.ConvertToInternalUnits(mm, UnitTypeId.Millimeters);
        }

        partial void OnCornerToleranceMmTextChanged(string value) => RebuildPreview();

        [RelayCommand]
        private void RefreshPreview() => RebuildPreview();

        /// <summary>Re-runs the full loop/nesting pipeline and repopulates the preview
        /// grid, metrics, and copyable text areas. Does not touch the model.</summary>
        private void RebuildPreview()
        {
            PreviewRows.Clear();
            HasRunSinceLastPreview = false;
            IsLoopsPreviewExpanded = true;

            var loopResult = GeometryLoopService.BuildLoops(_detailElements, ToleranceFeet(), AddLog);
            var roofGroups = GeometryLoopService.GroupByNesting(loopResult.ValidLoops, AddLog);
            _lastRoofGroups = roofGroups;

            foreach (var g in roofGroups)
            {
                double areaSqM = g.Outer.AreaInternal * 0.0929;
                foreach (var o in g.Openings) areaSqM -= o.AreaInternal * 0.0929;
                g.AreaDisplay = areaSqM;

                string statusKind = g.Openings.Count > 0 ? "Opening" : (g.ContainsAutoClosed ? "AutoClosed" : "Ready");
                string statusLabel = g.Openings.Count > 0 ? "Boundary + Opening"
                    : g.ContainsAutoClosed ? "Auto-Closed"
                    : g.ContainsCornerTrimmed ? "Corner-Trimmed"
                    : "Ready";

                PreviewRows.Add(new RoofPreviewRow
                {
                    RoofNumber = g.RoofNumber,
                    StatusLabel = statusLabel,
                    StatusKind = statusKind,
                    LevelName = _level.Name,
                    BoundaryLineIds = string.Join(", ", g.Outer.SourceLineIds.Distinct().Select(i => i.Value)),
                    OpeningLineIds = g.Openings.Count > 0
                        ? string.Join(", ", g.Openings.SelectMany(o => o.SourceLineIds).Distinct().Select(i => i.Value))
                        : "—",
                    AreaSqM = areaSqM,
                    Group = g
                });
            }

            // ── Metrics ──────────────────────────────────────────────────────────
            MetricClosedLoops = loopResult.ValidLoops.Count;
            MetricAutoClosed = loopResult.ValidLoops.Count(l => l.WasAutoClosed);
            MetricCornerTrimmed = loopResult.ValidLoops.Count(l => l.WasCornerTrimmed && !l.WasAutoClosed);
            MetricSkipped = loopResult.SkippedGroups.Count;
            MetricStrandedElements = loopResult.SkippedGroups.Sum(s => s.LineIds.Distinct().Count());
            MetricRoofsToCreate = roofGroups.Count;
            MetricWithOpenings = roofGroups.Count(g => g.Openings.Count > 0);

            // ── Copyable outputs ─────────────────────────────────────────────────
            var autoClosedLineIds = loopResult.ValidLoops
                .Where(l => l.WasAutoClosed)
                .SelectMany(l => l.SourceLineIds)
                .Distinct()
                .Select(i => i.Value)
                .ToList();
            AutoClosedLinesText = autoClosedLineIds.Count > 0 ? string.Join(", ", autoClosedLineIds) : "(none)";

            RoofsFromAutoClosedText = "(populated after Run)";
            AllCreatedRoofsText = "(populated after Run)";
            RoofsWithOpeningsText = "(populated after Run)";

            var skippedLineIds = loopResult.SkippedGroups.SelectMany(s => s.LineIds).Distinct().Select(i => i.Value).ToList();
            SkippedLinesText = skippedLineIds.Count > 0 ? string.Join(", ", skippedLineIds) : "(none)";

            RunCommand.NotifyCanExecuteChanged();
        }

        private bool CanRun() => !IsBusy && !HasRunSinceLastPreview && SelectedRoofType != null && _lastRoofGroups.Count > 0;

        [RelayCommand(CanExecute = nameof(CanRun))]
        private void Run()
        {
            IsBusy = true;
            RunCommand.NotifyCanExecuteChanged();

            _handler.PendingRequest = new CreateRoofsRunRequest
            {
                RoofGroups = _lastRoofGroups,
                RoofTypeId = SelectedRoofType.Id,
                Level = _level,
                DeleteSourceLines = DeleteSourceLines
            };

            _externalEvent.Raise();
        }

        /// <summary>Called by the handler on the Revit API thread after the run completes.
        /// Window stays open (per confirmed spec) so the user can review the log and
        /// copyable outputs, then optionally Refresh Preview and run again.</summary>
        public void OnRunComplete(RunSummary summary, List<ElementId> createdRoofIds, List<ElementId> autoClosedRoofIds, List<ElementId> roofsWithOpeningsIds)
        {
            IsBusy = false;

            // Confirmed spec: Run stays disabled after completion — only Refresh Preview
            // (which re-validates the model against current geometry) re-enables it.
            HasRunSinceLastPreview = true;

            // Confirmed spec: Roof Loops Preview collapses after a successful run —
            // its data is now stale (pre-run state), so results live in Element ID
            // Outputs / Activity Log / footer summary instead until Refresh Preview.
            IsLoopsPreviewExpanded = false;

            RoofsFromAutoClosedText = autoClosedRoofIds.Count > 0
                ? string.Join(", ", autoClosedRoofIds.Select(i => i.Value))
                : "(none)";

            AllCreatedRoofsText = createdRoofIds.Count > 0
                ? string.Join(", ", createdRoofIds.Select(i => i.Value))
                : "(none)";

            RoofsWithOpeningsText = roofsWithOpeningsIds.Count > 0
                ? string.Join(", ", roofsWithOpeningsIds.Select(i => i.Value))
                : "(none)";

            SummaryText = $"{summary.RoofsCreatedCount} roofs created | {summary.AutoClosedLoopCount} auto-closed | " +
                          $"{summary.CornerTrimmedLoopCount} trimmed | {summary.FailedCount} failed | " +
                          $"{MetricStrandedElements} stranded ({MetricSkipped} groups)" +
                          (summary.SourceLinesDeletedCount > 0 ? $" | {summary.SourceLinesDeletedCount} source lines deleted" : "");

            AddLog(LogLevel.Info, $"Run completed — {SummaryText}");

            SaveSettings("after run");
            RunCommand.NotifyCanExecuteChanged();
        }

        public void SaveSettings(string reason)
        {
            _settings.LastRoofTypeName = SelectedRoofType?.Name;
            if (double.TryParse(CornerToleranceMmText, out double mm) && mm >= 0)
                _settings.CornerToleranceMm = mm;
            _settings.DeleteSourceLines = DeleteSourceLines;

            bool ok = SettingsService.Save(_settings, out string msg);
            AddLog(ok ? LogLevel.Info : LogLevel.Warning, $"{msg} ({reason}).");
        }

        public void AddLog(LogLevel level, string message) => Logs.Add(new LogEntry(level, message));

        public void ShowCriticalError(string reason)
        {
            TaskDialog.Show("Roof From Detail Lines",
                $"The operation could not complete cleanly.\n\n{reason}");
        }

        [RelayCommand]
        private void CopyAutoClosedLines() { Clipboard.SetText(AutoClosedLinesText); ShowToast("Auto-closed line IDs copied"); }

        [RelayCommand]
        private void CopyRoofsFromAutoClosed() { Clipboard.SetText(RoofsFromAutoClosedText); ShowToast("Roof IDs copied"); }

        [RelayCommand]
        private void CopySkippedLines() { Clipboard.SetText(SkippedLinesText); ShowToast("Skipped line IDs copied"); }

        [RelayCommand]
        private void CopyAllCreatedRoofs() { Clipboard.SetText(AllCreatedRoofsText); ShowToast("All created roof IDs copied"); }

        [RelayCommand]
        private void CopyRoofsWithOpenings() { Clipboard.SetText(RoofsWithOpeningsText); ShowToast("Roof-with-openings IDs copied"); }

        [RelayCommand]
        private void CopyAllLogs()
        {
            var sb = new StringBuilder();
            foreach (var l in Logs) sb.AppendLine(l.ToString());
            Clipboard.SetText(sb.ToString());
            ShowToast("Logs copied to clipboard");
        }

        public void ShowToast(string message)
        {
            ToastMessage = message;
            var timer = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromSeconds(2) };
            timer.Tick += (s, e) => { ToastMessage = ""; timer.Stop(); };
            timer.Start();
        }

        [RelayCommand]
        private void ExportLogs()
        {
            var dialog = new SaveFileDialog
            {
                Filter = "Text file (*.txt)|*.txt",
                FileName = $"RoofFromDetailLines_Logs_{DateTime.Now:yyyy-MM-dd_HH-mm}.txt"
            };
            if (dialog.ShowDialog() != true) return;

            var sb = new StringBuilder();
            foreach (var l in Logs) sb.AppendLine(l.ToString());
            sb.AppendLine();
            if (HasSummary) sb.AppendLine("Run summary: " + SummaryText);
            sb.AppendLine();
            sb.AppendLine("Auto-Closed Lines: " + AutoClosedLinesText);
            sb.AppendLine("Roofs From Auto-Closed Loops: " + RoofsFromAutoClosedText);
            sb.AppendLine("Skipped Lines: " + SkippedLinesText);
            sb.AppendLine("All Created Roofs: " + AllCreatedRoofsText);
            sb.AppendLine("Roofs With Openings: " + RoofsWithOpeningsText);

            File.WriteAllText(dialog.FileName, sb.ToString());
        }

        // Copy Selected (log rows) handled in code-behind, per suite convention.
    }
}
