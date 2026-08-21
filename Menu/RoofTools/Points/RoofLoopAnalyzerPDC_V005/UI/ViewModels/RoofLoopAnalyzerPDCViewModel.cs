// =======================================================
// File: RoofLoopAnalyzerPDCViewModel.cs
// Location: UI/ViewModels/
// Changes vs V004:
//   Converted from manual SetProperty/hand-wired IRelayCommand pattern to
//   [ObservableProperty]/[RelayCommand] source-generator attributes, per
//   this suite's stack convention. Method names AnalyzeRoof/ApplyDivisions
//   renamed to Analyze/ApplyDivision so the generated command names
//   (AnalyzeCommand/ApplyDivisionCommand) match the existing XAML
//   bindings exactly — no XAML changes needed for these two.
//   REMOVED LogText (plain string log) and AppendLog(string)/InferLevel —
//   LogText was written to but never bound/displayed anywhere in the
//   XAML (dead code); the emoji-sniffing InferLevel heuristic is no
//   longer needed now that every call site specifies LogLevel directly,
//   matching every other tool's logging convention. LogEntries
//   (ObservableCollection<LogEntry>) is now the single log surface.
//   The ExternalEvent/RaiseRequest plumbing (already correct — every
//   Revit API call already routed through IExternalEventHandler) is
//   unchanged.
// =======================================================

using Autodesk.Revit.DB;
using Revit26_Plugin.RoofLoopAnalyzerPDC.V005.Infrastructure.Helpers;
using Revit26_Plugin.RoofLoopAnalyzerPDC.V005.Infrastructure.ExternalEvents;
using Revit26_Plugin.Shared.Models;
using Autodesk.Revit.UI;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Revit26_Plugin.RoofLoopAnalyzerPDC.V005.Core.Models;
using Revit26_Plugin.RoofLoopAnalyzerPDC.V005.Core.Services;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace Revit26_Plugin.RoofLoopAnalyzerPDC.V005.UI.ViewModels
{
    public partial class RoofLoopAnalyzerPDCViewModel : ObservableObject
    {
        private readonly Document           _doc;
        private readonly RoofBase           _roof;
        private readonly RoofGeometryService _geometryService;
        private readonly LoopDivisionService _divisionService;

        // ── Loops DataGrid ───────────────────────────────────────────────────────
        public ObservableCollection<RoofLoopModel> Loops { get; set; }

        // ── Log (project standard) ───────────────────────────────────────────────
        public ObservableCollection<LogEntry> LogEntries { get; } = new ObservableCollection<LogEntry>();

        // ── Global point-count dropdown ──────────────────────────────────────────
        public List<int> PointCountOptions { get; } = new List<int> { 4, 6, 8, 10, 12, 14, 16 };

        [ObservableProperty]
        private int selectedPointCount = 8;

        partial void OnSelectedPointCountChanged(int value)
        {
            // Keep all loop models in sync with the global selection
            foreach (var loop in Loops)
                loop.RecommendedPoints = value;
        }

        // ─────────────────────────────────────────────────────────────────────────
        public RoofLoopAnalyzerPDCViewModel(Document doc, RoofBase roof)
        {
            LoadToolSettings();
            _doc             = doc;
            _roof            = roof;
            _geometryService = new RoofGeometryService();
            _divisionService = new LoopDivisionService();
            Loops            = new ObservableCollection<RoofLoopModel>();
        }

        // ── Analyze ───────────────────────────────────────────────────────────────
        public void ExecuteAnalyzeRoofCore()
        {
            Loops.Clear();
            AddLog(LogLevel.Info, "Analyzing roof geometry...");

            List<RoofLoopModel> loops;
            try
            {
                loops = _geometryService.ExtractCircularLoops(_roof);
            }
            catch (Exception ex)
            {
                AddLog(LogLevel.Error, $"Geometry extraction failed: {ex.Message}");
                return;
            }

            var innerLoops = loops.Where(l => l.LoopType == "Inner").ToList();

            foreach (var loop in innerLoops)
            {
                loop.RecommendedPoints = SelectedPointCount;
                loop.IsSelected        = true;
                Loops.Add(loop);
            }

            int total      = Loops.Count;
            int circular   = Loops.Count(l => l.LoopShapeType == "Circular");
            int rectangles = Loops.Count(l => l.LoopShapeType == "Rectangle");
            int others     = Loops.Count(l => l.LoopShapeType == "Other");

            AddLog(LogLevel.Success, $"Analysis complete — Inner Loops: {total}");
            AddLog(LogLevel.Info, $"Circular: {circular}  |  Rectangle: {rectangles}  |  Other: {others}");

            if (total == 0)
                AddLog(LogLevel.Warning, "No inner loops found on the selected roof.");
        }

        // ── Apply divisions ───────────────────────────────────────────────────────
        public void ExecuteApplyDivisionsCore()
        {
            var validLoops = Loops.Where(l => l.IsSelected && l.RecommendedPoints > 0).ToList();

            if (!validLoops.Any())
            {
                AddLog(LogLevel.Warning, "Apply skipped — no loops are selected.");
                return;
            }

            AddLog(LogLevel.Info, $"Applying {SelectedPointCount} division points to {validLoops.Count} loop(s)...");

            List<LogEntry> serviceLog = _divisionService.AddDivisionPoints(_doc, _roof, validLoops);
            foreach (var entry in serviceLog)
                AddLog(entry);

            int totalAdded = validLoops.Sum(l => l.RecommendedPoints);
            AddLog(LogLevel.Info, $"Done. Requested {totalAdded} point(s) across {validLoops.Count} loop(s).");
        }

        // ── ExternalEvent plumbing (project rule: every Revit API call goes
        //    through IExternalEventHandler / ExternalEvent.Raise) ─────────────
        private ExternalEvent _externalEvent;
        private RoofLoopAnalyzerPDCEventHandler _eventHandler;

        /// <summary>Wired by the IExternalCommand after ExternalEvent.Create().</summary>
        public void SetExternalEvent(ExternalEvent externalEvent, RoofLoopAnalyzerPDCEventHandler handler)
        {
            _externalEvent = externalEvent;
            _eventHandler  = handler;
        }

        private void RaiseRequest(RoofLoopAnalyzerPDCRequest request)
        {
            if (_externalEvent == null || _eventHandler == null)
            {
                AddLog(LogLevel.Error, "Internal error: external event not initialised.");
                return;
            }
            _eventHandler.PendingRequest = request;
            var status = _externalEvent.Raise();
            if (status != ExternalEventRequest.Accepted)
                AddLog(LogLevel.Warning,
                    $"Revit refused the request (status: {status}). Try again when Revit is idle.");
        }

        /// <summary>UI entry point — queues the Revit work onto Revit's own thread
        /// via the tool's ExternalEvent. Never touches the Revit API directly.</summary>
        [RelayCommand]
        private void Analyze() => RaiseRequest(RoofLoopAnalyzerPDCRequest.AnalyzeRoof);

        /// <summary>UI entry point — queues the Revit work onto Revit's own thread
        /// via the tool's ExternalEvent. Never touches the Revit API directly.</summary>
        [RelayCommand]
        private void ApplyDivision() => RaiseRequest(RoofLoopAnalyzerPDCRequest.ApplyDivisions);

        // ── Log helpers ───────────────────────────────────────────────────────
        private void AddLog(LogLevel level, string message) => AddLog(new LogEntry(level, message));

        private void AddLog(LogEntry entry) => LogEntries.Add(entry);

        /// <summary>Called by the tool's IExternalEventHandler (Revit thread).
        /// Marshals to the UI thread before touching the bound collection.</summary>
        public void LogFromHandler(LogLevel level, string message)
        {
            var dispatcher = System.Windows.Application.Current?.Dispatcher;
            if (dispatcher == null || dispatcher.CheckAccess()) AddLog(level, message);
            else dispatcher.BeginInvoke(new Action(() => AddLog(level, message)));
        }

        // ── Log clipboard / export (project standard) ─────────────────────────
        /// <summary>Bound from the log ListBox so "Copy Selected" knows the selection.</summary>
        public IList SelectedLogEntries { get; set; }

        [RelayCommand]
        private void CopyAllLogs()
        {
            if (LogEntries.Count == 0) return;
            System.Windows.Clipboard.SetText(
                string.Join(Environment.NewLine, LogEntries.Select(e => e.ToString())));
        }

        [RelayCommand]
        private void CopySelectedLogs()
        {
            if (SelectedLogEntries == null || SelectedLogEntries.Count == 0) { CopyAllLogs(); return; }
            System.Windows.Clipboard.SetText(string.Join(Environment.NewLine,
                SelectedLogEntries.Cast<LogEntry>().Select(e => e.ToString())));
        }

        /// <summary>Manual export — {ToolName}_Logs_{yyyy-MM-dd}_{HH-mm}.txt</summary>
        [RelayCommand]
        private void ExportLog()
        {
            var folder = LogFileService.ResolveLogFolder(_exportFolderPath, m => LogFromHandler(LogLevel.Warning, m));
            if (string.IsNullOrEmpty(folder)) return;
            var path = LogFileService.Write(folder, LogEntries, m => LogFromHandler(LogLevel.Error, m));
            if (!string.IsNullOrEmpty(path)) LogFromHandler(LogLevel.Success, $"Log exported to: {path}");
        }

        /// <summary>Auto-save on completion, per project logging rules.</summary>
        public void AutoSaveLog()
        {
            var folder = LogFileService.ResolveLogFolder(_exportFolderPath, null);
            if (string.IsNullOrEmpty(folder)) return;
            var path = LogFileService.Write(folder, LogEntries, null);
            if (!string.IsNullOrEmpty(path)) LogFromHandler(LogLevel.Info, $"Log auto-saved to: {path}");
        }

        // ── Settings ─────────────────────────────────────────────────────────
        private string _exportFolderPath;

        private void LoadToolSettings()
        {
            var s = SettingsService.Load(m => LogFromHandler(LogLevel.Warning, m));
            if (s == null) return;
            if (!string.IsNullOrWhiteSpace(s.ExportFolderPath)) _exportFolderPath = s.ExportFolderPath;
            LogFromHandler(LogLevel.Info, "Settings loaded from disk.");
        }

        /// <summary>Called by the View code-behind on close.</summary>
        public void SaveSettingsOnClose()
        {
            SettingsService.Save(new RoofLoopAnalyzerPDCSettings { ExportFolderPath = _exportFolderPath },
                                 m => LogFromHandler(LogLevel.Warning, m));
        }
    }
}
