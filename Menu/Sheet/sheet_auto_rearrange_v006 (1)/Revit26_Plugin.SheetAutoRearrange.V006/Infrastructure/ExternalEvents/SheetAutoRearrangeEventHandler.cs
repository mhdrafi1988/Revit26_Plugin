using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Revit26_Plugin.SheetAutoRearrange.V006.Core.Engine;
using Revit26_Plugin.SheetAutoRearrange.V006.Core.Models;
using Revit26_Plugin.SheetAutoRearrange.V006.Core.Services;
using Revit26_Plugin.Shared.Models;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace Revit26_Plugin.SheetAutoRearrange.V006.Infrastructure.ExternalEvents
{
    /// <summary>Routes for the single external event pair, per suite orchestrator convention.</summary>
    public enum SheetAutoRearrangeAction
    {
        LoadViewsOnSheet,
        RunRearrange,

        /// <summary>V006: re-runs title block detection only, without reloading the views grid. Used by the "Re-detect" button.</summary>
        RedetectRegion
    }

    /// <summary>
    /// Single IExternalEventHandler for Sheet Auto Rearrange. The ViewModel
    /// sets Action + the required request fields, then calls
    /// ExternalEvent.Raise(); Execute() dispatches based on Action.
    ///
    /// V006 CHANGE: usable-area resolution moved from a raw sheet-bbox lookup
    /// to TitleBlockDetectionService, which classifies the title block and
    /// returns a PlaceableRegion (single rect or L-shape). Manual fallback
    /// (Undetected mode) is supplied by the ViewModel via
    /// ManualRegionOverride and takes priority over auto-detection when set.
    /// </summary>
    public class SheetAutoRearrangeEventHandler : IExternalEventHandler
    {
        public SheetAutoRearrangeAction Action { get; set; }

        // ── LoadViewsOnSheet / RedetectRegion request/response ─────────
        public ViewSheet? TargetSheet { get; set; }
        public List<ViewOnSheetItem>? LoadedItems { get; private set; }

        /// <summary>Resolved placeable region, or null if the sheet has 2+ title blocks (caller must skip Run entirely).</summary>
        public PlaceableRegion? Region { get; private set; }

        /// <summary>True if detection found 2+ title blocks on the sheet — Run must be blocked, per confirmed design.</summary>
        public bool MultipleTitleBlocksFound { get; private set; }

        /// <summary>True if no title block instance exists on the sheet at all.</summary>
        public bool NoTitleBlockFound { get; private set; }

        /// <summary>
        /// When set (non-null) by the ViewModel BEFORE Raise(), this manual
        /// rectangle (mm, sheet space) is used instead of auto-detection —
        /// covers both the Undetected fallback and any future user override.
        /// Cleared by the ViewModel after a successful auto re-detect.
        /// </summary>
        public (double minXMm, double minYMm, double maxXMm, double maxYMm)? ManualRegionOverride { get; set; }

        // ── RunRearrange request/response ──────────────────────────────
        public List<ViewOnSheetItem>? ItemsToProcess { get; set; }
        public RearrangeAlgorithm Algorithm { get; set; }
        public OverflowHandlingMode OverflowHandlingMode { get; set; }
        public GapSettings? GapSettings { get; set; }
        public double RowToleranceMm { get; set; }
        public RowAlignment RowAlignment { get; set; }
        public BlockAlignmentH BlockAlignmentH { get; set; }
        public BlockAlignmentV BlockAlignmentV { get; set; }
        public ObservableCollection<LogEntry>? Log { get; set; }
        public RunResult? LastRunResult { get; private set; }

        /// <summary>Raised on the UI thread after Execute() completes, regardless of Action.</summary>
        public event Action? Completed;

        private readonly ViewportCollectorService _collector = new();
        private readonly TitleBlockDetectionService _detectionService = new();
        private readonly RearrangeEngine _engine = new();

        public void Execute(UIApplication app)
        {
            var doc = app.ActiveUIDocument.Document;

            try
            {
                switch (Action)
                {
                    case SheetAutoRearrangeAction.LoadViewsOnSheet:
                        ExecuteLoad(doc);
                        break;

                    case SheetAutoRearrangeAction.RedetectRegion:
                        ExecuteDetectRegion(doc);
                        break;

                    case SheetAutoRearrangeAction.RunRearrange:
                        ExecuteRun(doc);
                        break;
                }
            }
            finally
            {
                Completed?.Invoke();
            }
        }

        private void ExecuteLoad(Document doc)
        {
            if (TargetSheet == null)
            {
                LoadedItems = new List<ViewOnSheetItem>();
                Region = null;
                return;
            }

            LoadedItems = _collector.CollectViewsOnSheet(doc, TargetSheet);
            ExecuteDetectRegion(doc);
        }

        private void ExecuteDetectRegion(Document doc)
        {
            if (TargetSheet == null)
            {
                Region = null;
                return;
            }

            // Manual override takes priority — user has explicitly supplied
            // coordinates (either because auto-detect returned Undetected, or
            // they chose to override a successful auto-detect).
            if (ManualRegionOverride.HasValue)
            {
                var m = ManualRegionOverride.Value;
                Region = _detectionService.BuildManualRegion(m.minXMm, m.minYMm, m.maxXMm, m.maxYMm);
                MultipleTitleBlocksFound = false;
                NoTitleBlockFound = false;
                return;
            }

            var detection = _detectionService.Detect(doc, TargetSheet);
            MultipleTitleBlocksFound = detection.MultipleTitleBlocksFound;
            NoTitleBlockFound = detection.NoTitleBlockFound;
            Region = detection.Region;
        }

        private void ExecuteRun(Document doc)
        {
            if (TargetSheet == null || ItemsToProcess == null || GapSettings == null || Log == null)
            {
                LastRunResult = new RunResult { Success = false, ErrorMessage = "Missing required Run parameters." };
                return;
            }

            // Re-resolve the region at Run time (not reusing a stale value from
            // Load) in case the sheet's title block changed since the window
            // opened. Same manual-override-takes-priority rule applies.
            ExecuteDetectRegion(doc);

            if (MultipleTitleBlocksFound)
            {
                LastRunResult = new RunResult
                {
                    Success = false,
                    ErrorMessage = "Sheet has multiple title blocks — Run skipped, no changes made."
                };
                return;
            }

            LastRunResult = _engine.Run(
                doc,
                TargetSheet,
                ItemsToProcess,
                Algorithm,
                OverflowHandlingMode,
                GapSettings,
                RowToleranceMm,
                RowAlignment,
                BlockAlignmentH,
                BlockAlignmentV,
                Region,
                Log);
        }

        public string GetName() => "Sheet Auto Rearrange Event Handler";
    }
}
